# Project Structure (Machine-Readable)

## Core Libraries

### asm-tools-lib
- **Path**: `VS/CSHARP/asm-tools-lib/asm-tools-lib.csproj`
- **Target**: net10.0
- **Namespace**: AsmTools
- **Dependencies**: asm-sim-lib
- **Key Types**: 
  - `AsmLanguageServerOptions` - LSP configuration
  - `Mnemonic`, `Arch`, `MicroArch` - Enumerations
  - `AsmSourceTools`, `RegisterTools`, `FlagTools` - Utility classes

### asm-sim-lib
- **Path**: `VS/CSHARP/asm-sim-lib/asm-sim-lib.csproj`
- **Target**: net10.0-windows
- **Namespace**: asm_sim_lib2
- **Dependencies**: asm-tools-lib, Microsoft.Z3 (4.16.0), QuikGraph (2.5.0)
- **Key Types**:
  - `State`, `StateUpdate`, `StaticFlow`, `DynamicFlow` - Simulation state
  - `OpcodeBase` and derived classes - Instruction implementations
  - `BranchInfo`, `ExecutionTree` - Control flow analysis
  - `ToolsZ3`, `ToolsFlags` - Z3 integration helpers

### asm-dude2-ls-lib
- **Path**: `VS/CSHARP/asm-dude2-ls-lib/asm-dude2-ls-lib.csproj`
- **Target**: net10.0-windows
- **Namespace**: AsmDude2LS
- **Dependencies**: asm-tools-lib, asm-sim-lib, Microsoft.VisualStudio.LanguageServer.Protocol (18.5.3), StreamJsonRpc (2.25.9)
- **Key Types**:
  - `LanguageServer` - LSP server implementation
  - `MnemonicStore`, `PerformanceStore` - Data stores
  - `AsmSignatureInformation` - Signature data
  - `ColorJsonConverter` - JSON serialization

## Build Order
1. asm-tools-lib (base)
2. asm-sim-lib (depends on asm-tools-lib)
3. asm-dude2-ls-lib (depends on asm-tools-lib, asm-sim-lib)

## Project References
- asm-tools-lib → asm-sim-lib
- asm-sim-lib → asm-tools-lib
- asm-dude2-ls-lib → asm-tools-lib, asm-sim-lib
