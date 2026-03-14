# Implementation Sketch: asm/getProvenStates LSP Method

## Overview

Expose Z3-proven register/flag states to external tools and AIs via a custom LSP method.

**Effort**: Low (60-80 lines of code)
**Dependencies**: None (uses existing simulator cache)
**Blocks**: Tool integration, AI context injection

---

## 1. Request/Response Types

### Add to LanguageServer.cs or new file `ProvenStatesTypes.cs`:

```csharp
namespace AsmDude2LS
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request to get Z3-proven register/flag states for a document URI and line range.
    /// </summary>
    internal class GetProvenStatesParams
    {
        /// <summary>
        /// Document URI (e.g., "file:///test.asm")
        /// </summary>
        public required string Uri { get; init; }

        /// <summary>
        /// Optional line range [startLine, endLine] inclusive. If null, entire document.
        /// </summary>
        public int[]? LineRange { get; init; }

        /// <summary>
        /// If true, include full state strings. If false, only return changed registers.
        /// Default: true
        /// </summary>
        public bool IncludeFullState { get; init; } = true;
    }

    /// <summary>
    /// Proven state at a single line.
    /// </summary>
    internal class ProvenLineState
    {
        /// <summary>
        /// Line number (0-indexed)
        /// </summary>
        public required int Line { get; init; }

        /// <summary>
        /// Register/flag state BEFORE executing this line (e.g., "RAX=0x1234 | RBX=0 | ZF=0")
        /// Null if not computed (blank line, comment, etc.)
        /// </summary>
        public string? BeforeState { get; init; }

        /// <summary>
        /// Register/flag state AFTER executing this line.
        /// Null if not computed.
        /// </summary>
        public string? AfterState { get; init; }

        /// <summary>
        /// Proof method. Currently always "Z3 SimpleStep"
        /// </summary>
        public required string ProvenBy { get; init; }

        /// <summary>
        /// Confidence level:
        /// - "complete" — fully proven (all paths explored)
        /// - "partial" — may be incomplete (solver timeout)
        /// - "unknown" — unable to compute
        /// </summary>
        public required string Confidence { get; init; }
    }

    /// <summary>
    /// Response: array of proven states per line.
    /// </summary>
    internal class ProvenStatesResponse
    {
        /// <summary>
        /// Proven states, indexed by line number.
        /// Empty list if no data available for document.
        /// </summary>
        public required List<ProvenLineState> States { get; init; }

        /// <summary>
        /// Metadata: when the states were computed (ISO 8601)
        /// </summary>
        public string ComputedAt { get; init; } = DateTime.UtcNow.ToString("O");

        /// <summary>
        /// Total lines in document.
        /// </summary>
        public int TotalLines { get; init; }
    }
}
```

---

## 2. Handler in LanguageServerTarget.cs

Add after the existing `asm/codeLensData` handler (around line 357):

```csharp
    [JsonRpcMethod("asm/getProvenStates", UseSingleObjectParameterDeserialization = true)]
    public ProvenStatesResponse? GetProvenStates(GetProvenStatesParams parameter)
    {
        LanguageServer.LogInfo($"GetProvenStates: uri={parameter.Uri}, lineRange={parameter.LineRange?[0]}-{parameter.LineRange?[1]}");
        var result = this.server.GetProvenStates(parameter);
        LanguageServer.LogInfo($"GetProvenStates: stateCount={result?.States?.Count ?? 0}");
        return result;
    }
```

---

## 3. Implementation in LanguageServer.cs

Add to the `LanguageServer` class (after other `GetXxx` methods):

```csharp
        /// <summary>
        /// Retrieve Z3-proven register/flag states for a document.
        /// Returns all states in the line range, or null if document not found.
        /// </summary>
        internal ProvenStatesResponse? GetProvenStates(GetProvenStatesParams param)
        {
            try
            {
                var uri = new Uri(param.Uri);
                int totalLines = 0;

                lock (this.textDocuments)
                {
                    if (this.textDocuments.TryGetValue(uri.ToString(), out var doc))
                    {
                        totalLines = doc.Length;
                    }
                }

                if (totalLines == 0)
                {
                    LanguageServer.LogWarning($"GetProvenStates: document not found: {param.Uri}");
                    return new ProvenStatesResponse { States = [], TotalLines = 0 };
                }

                // Determine line range
                int startLine = param.LineRange?[0] ?? 0;
                int endLine = param.LineRange?[1] ?? (totalLines - 1);
                startLine = Math.Max(0, startLine);
                endLine = Math.Min(totalLines - 1, endLine);

                var states = new List<ProvenLineState>();

                // Iterate through line range and query simulator cache
                for (int lineNum = startLine; lineNum <= endLine; lineNum++)
                {
                    // Query before/after states from simulator
                    string? beforeState = this.asmSimulator_.GetRegisterStatesBeforeLine(uri, lineNum);
                    string? afterState = this.asmSimulator_.GetRegisterStatesAfterLine(uri, lineNum);

                    // Only add states that exist (skip blank lines/comments with null)
                    if (beforeState != null || afterState != null)
                    {
                        states.Add(new ProvenLineState
                        {
                            Line = lineNum,
                            BeforeState = beforeState,
                            AfterState = afterState,
                            ProvenBy = "Z3 SimpleStep",
                            Confidence = "complete"  // TODO: add confidence level from diagnostics
                        });
                    }
                }

                return new ProvenStatesResponse
                {
                    States = states,
                    TotalLines = totalLines,
                    ComputedAt = DateTime.UtcNow.ToString("O")
                };
            }
            catch (Exception ex)
            {
                LanguageServer.LogError($"GetProvenStates: exception: {ex.Message}");
                return null;
            }
        }
```

---

## 4. Client Usage Examples

### Example 1: Query entire document via curl

```bash
curl -X POST http://localhost:7000/asm/getProvenStates \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "asm/getProvenStates",
    "params": {
      "uri": "file:///Users/henk/test.asm"
    }
  }' | jq .
```

### Example 2: Query specific line range in Python

```python
import json
import subprocess

def query_proven_states(uri, start_line=0, end_line=50):
    """Query Z3-proven states for a file."""
    request = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "asm/getProvenStates",
        "params": {
            "uri": uri,
            "lineRange": [start_line, end_line]
        }
    }

    # Send to running LSP server
    result = subprocess.run(
        ["curl", "-s", "-X", "POST", "http://localhost:7000",
         "-H", "Content-Type: application/json",
         "-d", json.dumps(request)],
        capture_output=True, text=True
    )

    response = json.loads(result.stdout)
    return response["result"]["states"]

# Usage
states = query_proven_states("file:///test.asm", start_line=0, end_line=10)
for state in states:
    print(f"Line {state['line']}:")
    print(f"  Before: {state['beforeState']}")
    print(f"  After:  {state['afterState']}")
```

### Example 3: AI/Tool Integration (pseudo-code)

```python
# LLM receives assembly code + proven states
assembly_code = """
mov rax, 0x1234
add rax, 0x100
cmp rax, 0x1334
"""

proven_states = query_proven_states("file:///test.asm")

# Inject proven context into LLM prompt
prompt = f"""
Analyze this assembly code. Here are the Z3-proven register values:

Code:
{assembly_code}

Proven States:
{json.dumps(proven_states, indent=2)}

Suggestions based on proven values:
- Line 2: RAX is proven to equal 0x1234, which remains constant after ADD
- Line 3: CMP is proven to always set ZF=1 (equal)

Suggest optimizations:
"""

# Send to LLM, get suggestions grounded in proof
```

---

## 5. Integration Points

### For Tools/AIs to discover this capability:

1. **ServerCapabilities**: Option to advertise method in `initialize` response
   - Add to custom capabilities in `OnInitialize` (LanguageServerTarget.cs)
   - Clients can check if `provenStatesProvider` is available

2. **Documentation**: Update extension README.md
   - Document the new `asm/getProvenStates` method
   - Show JSON request/response format
   - List use cases

3. **Testing**: Add integration test
   - Call method with test file
   - Verify states match expected Z3 output
   - Check line range filtering works

---

## 6. Future Enhancements

### Phase 2: Confidence Levels
- Query simulator diagnostics (`GetDiagnostics()`) to set confidence
- "complete" if no timeout/errors
- "partial" if Z3 timeout occurred
- "unknown" if syntax error on line

### Phase 3: Diff Mode
- Optional `baselineUri` parameter to return only changed states
- Useful for comparing branches: "what changed from v1 to v2?"

### Phase 3: Proof Justification
- Return WHY a state is proven (e.g., "RAX=0x1234 because line 1 is `mov rax, 0x1234`")
- Useful for audit trails and AI reasoning

---

## 7. Code Locations Summary

| File | Lines | Change |
|------|-------|--------|
| `ProvenStatesTypes.cs` | NEW | Define request/response types |
| `LanguageServerTarget.cs` | +8 | Add `[JsonRpcMethod("asm/getProvenStates")]` handler |
| `LanguageServer.cs` | +45 | Implement `GetProvenStates()` method |
| **Total** | ~60 lines | Complete implementation |

---

## 8. Testing Checklist

- [ ] Compile without errors
- [ ] Method discoverable via LSP introspection
- [ ] Returns empty array for non-existent document
- [ ] Returns all lines if `lineRange` null
- [ ] Returns filtered lines if `lineRange` [10, 20]
- [ ] States match direct calls to `GetRegisterStatesBeforeLine/After`
- [ ] Handles edge cases: line 0, last line, out-of-range lines
- [ ] No exceptions on concurrent document changes
