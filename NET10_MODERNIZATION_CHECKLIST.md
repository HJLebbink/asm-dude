# .NET 10 Modernization Checklist

## Completed Fixes

### ✅ Project Structure
- [x] Removed circular dependency (asm-tools-lib no longer depends on asm-sim-lib)
- [x] Fixed project references

### ✅ Code Modernization
- [x] Converted public fields to auto-properties in AsmLanguageServerOptions
- [x] Fixed naming conventions (snake_case → PascalCase)
- [x] Removed SA1401 pragma warning disable
- [x] Simplified switch expressions
- [x] Removed commented-out code

## Remaining Issues

### High Priority
- [ ] Verify build succeeds after changes
- [ ] Update all usages of old property names throughout codebase
- [ ] Test serialization/deserialization still works
- [ ] Check for any breaking changes in LSP integration

### Medium Priority
- [ ] Add XML documentation to all public members
- [ ] Review nullable annotations
- [ ] Optimize performance-critical paths with Span<T>
- [ ] Update Z3 dependency to use version range

### Low Priority
- [ ] Modernize using directives (remove redundant ones)
- [ ] Add source generators where appropriate
- [ ] Consider record types for immutable data
- [ ] Update documentation

## Verification Steps

```bash
# Build all projects
dotnet build VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj
dotnet build VS/CSHARP/asm-sim-lib/asm-sim-lib.csproj
dotnet build VS/CSHARP/asm-dude2-ls-lib/asm-dude2-ls-lib.csproj

# Check for warnings
dotnet build /warn:4

# Run tests
dotnet test
```

## Breaking Changes

### Property Name Changes
The following property names have changed from snake_case to PascalCase:

| Old Name | New Name |
|----------|----------|
| `useAssemblerMasm` | `UseAssemblerMasm` |
| `useAssemblerNasm` | `UseAssemblerNasm` |
| `useAssemblerNasm_Att` | `UseAssemblerNasm_Att` |
| `useAssemblerAutoDetect` | `UseAssemblerAutoDetect` |
| `useAssemblerDisassemblyMasm` | `UseAssemblerDisassemblyMasm` |
| `useAssemblerDisassemblyNasm_Att` | `UseAssemblerDisassemblyNasm_Att` |
| `useAssemblerDisassemblyAutoDetect` | `UseAssemblerDisassemblyAutoDetect` |
| `ARCH_8086` | `Arch8086` |
| `ARCH_186` | `Arch186` |
| ... | ... |

**Impact**: Any code using these properties will need to be updated.

## Next Steps

1. **Apply all changes** to AsmLanguageServerOptions.cs
2. **Update all usages** throughout the codebase
3. **Build and test** to verify no breaking changes
4. **Update documentation** to reflect new property names
5. **Run full test suite** to ensure functionality
