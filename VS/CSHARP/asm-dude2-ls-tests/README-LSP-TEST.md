# LSP Test Client for AsmDude

This is a simple Node.js script to test the AsmDude LSP server.

## Prerequisites

1. Build AsmDude LSP server:
   ```bash
   dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj
   ```

2. Run the LSP server:
   ```bash
   # Build first
   dotnet build VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj
   
   # Run in stdio mode
   dotnet run --project VS\CSHARP\asm-dude2-ls\asm-dude2-ls.csproj -- --stdio
   ```

## Running the Test

1. Install dependencies:
   ```bash
   npm install
   ```

2. Run the test:
   ```bash
   node test_lsp.js
   ```

## Test Features

The test script will:
1. Start the AsmDude LSP server
2. Send a `initialize` request
3. Send a `textDocument/didOpen` request for a sample .asm file
4. Send a `textDocument/hover` request
5. Send a `textDocument/definition` request
6. Send a `textDocument/semanticTokens/full` request
7. Shutdown the server

## Expected Output

If successful, you should see LSP responses for each request including:
- Initialize response with capabilities
- Hover response with instruction documentation
- Definition response with label locations
- Semantic tokens for syntax highlighting
