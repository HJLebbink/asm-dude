# 📜 CODE STYLE GUIDE — .NET 10 (AsmDude2 / asm-*)

## 📌 General Principles
- **Prefer modern C# 12/13 features**: `primary constructors`, `file-scoped namespaces`, `target-typed new()`, `readonly struct`, `static abstract interface members`.
- **Avoid legacy patterns**: no `new MyClass() { Prop = x }` — use `with` or init-only properties.
- **No `var` for primitive types** (e.g., `int x = ...` not `var x = ...`) — improves readability in low-level code.
- **Use `Span<T>`/`ReadOnlySpan<T>`** for all string/byte parsing (you’re already doing this — ✅).
- **Prefer `params` arrays over `params ReadOnlySpan<T>`** only when interoperability matters — otherwise use `Span<T>`.

## 📌 Null & Safety
- ✅ Use `ArgumentNullException.ThrowIfNull(...)` (C# 10+).
- ✅ Enable `<Nullable>enable</Nullable>` — you already do.
- ❌ Avoid `object?` unless interop is required — prefer `T?` or `T?` with `where T : struct`.
- ✅ Use `#nullable enable` at top of every file — you already do.

## 📌 Async & Tasks
- ✅ Use `ValueTask` for hot-path methods (e.g., parsing, evaluation).
- ❌ Avoid `.Result` or `.Wait()` — use `await` everywhere.
  - ⚠️ **Exception**: `ExpressionEvaluator.Evaluate_Constant()` uses `.GetAwaiter().GetResult()` — acceptable *only* because Roslyn scripting requires synchronous entry point.
- ✅ Prefer `CSharpScript.RunAsync(...).GetAwaiter().GetResult()` over `CSharpScript.EvaluateAsync(...).Result`.

## 📌 Performance
- ✅ `Span<T>`, `stackalloc`, `MemoryExtensions.ToUpperInvariant()` — all great.
- ❌ Avoid `string.Concat`, `string.Replace`, `string.Split` — use `Span`-based parsing (you’re doing this).
- ✅ Use `static readonly` for lookup tables (e.g., mnemonic maps).
- ⚠️ `<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>` — consider removing globally; use `checked { }` only where needed.

## 📌 Z3 / SMT Integration
- ✅ Use `Context`, `Solver`, `BitVecExpr`, `BoolExpr` — good.
- ❌ Avoid `ToString()` on expressions in hot paths — cache keys instead.
- ✅ Use `ToolsZ3.ToStringHex(Tv[])` for debugging — keep.

## 📌 LSP / Language Server
- ✅ Use `Microsoft.VisualStudio.LanguageServer.Protocol` v18.5+ — modern.
- ✅ Use `[JsonRpcMethod(...)]` — good.
- ❌ Avoid `dynamic` — you’re not using it — ✅.
- ✅ Use `INotifyPropertyChanged` only for UI-bound data — not for core logic.

## 📌 Naming & Conventions
| Pattern | ✅ Good | ❌ Avoid |
|--------|--------|---------|
| Methods | `Parse_Constant`, `Evaluate_Constant` | `parseConstant`, `EvaluateConstant` (inconsistent) |
| Types | `PascalCase`, e.g., `ExpressionEvaluator`, `StateUpdate` | `snake_case`, `camelCase` |
| Constants | `kName`, `cName`, or `Name` (static readonly) | `CONSTANT_NAME` |
| Generics | `T`, `TValue`, `TKey` | `T1`, `T2`, `TValue1` |

## 📌 Conditional Compilation
- ✅ Use `#if NET10_0` — correct.
- ❌ Avoid `#if NET10_0_OR_GREATER` — not a standard symbol.
- ✅ Prefer `#if NET10_0` over `#if NETCOREAPP || NETFRAMEWORK`.

## 📌 XML Documentation
- ✅ Use `/// <summary>` — you do.
- ❌ Avoid `/// <remarks>` unless critical.
- ✅ Use `/// <param name="..."/>`, `/// <returns/>` — good.
- ⚠️ `CS1591` (missing XML) suppressed — consider enabling for public APIs only.

## 📌 Project Structure
- ✅ Separate libs: `asm-tools-lib`, `asm-sim-lib`, `asm-dude2-ls-lib` — clean.
- ✅ Use `<ProjectReference>` — good.
- ❌ Avoid `Assembly.LoadFrom` — you’re not using it — ✅.

---

## 🚫 CRITICAL RULE: NO DEAD/COMMENTED-OUT CODE

> **You must not commit or leave behind commented-out code, even if it was previously used or temporarily disabled.**

### ✅ Allowed
- Commented-out code *inside* a `#if DEBUG` or `#if TEST` block (e.g., for debugging).
- `// TODO: ...` or `// FIXME: ...` *only* if accompanied by a tracked issue/PR.

### ❌ Not Allowed
- `// return ctx.MkNot(ctx.MkBVAddNoOverflow(a, b, false));`  
- `// var result = ...;`  
- `/* var old = ...; */`  
- Any `//` or `/* */` block that contains *executable logic* or *previously active code*.

### ✅ What to do instead
1. **Delete** the commented-out code.
2. If you need to preserve it for historical/educational reasons, move it to a dedicated `Legacy/` folder (e.g., `Legacy/ExpressionEvaluator_Old.cs`) with a clear `README.md` explaining why.
3. If it’s part of a refactor, use **git history** — *never* keep it in the main codebase.

> 🔍 **Rationale**:  
> - Dead code increases cognitive load.  
> - It often becomes stale and misleading (e.g., API changes, Z3 version updates).  
> - It violates the **Boy Scout Rule**: *Leave the code cleaner than you found it*.
