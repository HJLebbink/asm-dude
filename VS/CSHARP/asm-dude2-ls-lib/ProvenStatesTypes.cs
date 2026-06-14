// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude2LS;

using System.Collections.Generic;

/// <summary>
/// Request to get Z3-proven register/flag states for a document URI and optional line range.
/// </summary>
public class GetProvenStatesParams
{
    /// <summary>
    /// Document URI (e.g., "file:///test.asm")
    /// </summary>
    public required string Uri { get; set; }

    /// <summary>
    /// Optional line range [startLine, endLine] inclusive. If null, returns entire document.
    /// </summary>
    public int[]? LineRange { get; set; }
}

/// <summary>
/// Proven state at a single line showing register/flag values before and after execution.
/// </summary>
public class ProvenLineState
{
    /// <summary>
    /// Line number (0-indexed)
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Register/flag state BEFORE executing this line (e.g., "RAX = 0x1234 | RBX = 0x0 | ZF = 0")
    /// Null if not computed (blank line, comment, etc.)
    /// </summary>
    public string? BeforeState { get; set; }

    /// <summary>
    /// Register/flag state AFTER executing this line.
    /// Null if not computed.
    /// </summary>
    public string? AfterState { get; set; }

    /// <summary>
    /// Proof method. Currently "Z3 SimpleStep"
    /// </summary>
    public required string ProvenBy { get; set; }

    /// <summary>
    /// Confidence level:
    /// - "complete" — fully proven (no timeouts)
    /// - "partial" — may be incomplete (solver timeout)
    /// - "unknown" — unable to compute
    /// </summary>
    public required string Confidence { get; set; }
}

/// <summary>
/// Response containing array of proven states per line for a document.
/// </summary>
public class ProvenStatesResponse
{
    /// <summary>
    /// Proven states, one entry per line with proven values. Empty list if no data available.
    /// </summary>
    public required List<ProvenLineState> States { get; init; }

    /// <summary>
    /// Total number of lines in the document.
    /// </summary>
    public int TotalLines { get; set; }

    /// <summary>
    /// Timestamp when states were computed (ISO 8601).
    /// </summary>
    public required string ComputedAt { get; set; }
}
