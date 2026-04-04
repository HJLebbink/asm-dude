import { spawn } from 'child_process';
import { Readable, Writable } from 'stream';

// Simple LSP test client
async function testAsmDudeLSP() {
    console.log('=== AsmDude LSP Test Client ===\n');

    // Start AsmDude LSP server in stdio mode
    const lspProcess = spawn('dotnet', [
        'run',
        '--project',
        'VS/CSHARP/asm-dude2-ls/asm-dude2-ls.csproj',
        '--',
        '--stdio'
    ], {
        cwd: 'C:/Source/Github/asm-dude',
        stdio: ['pipe', 'pipe', 'pipe'],
        shell: true
    });

    console.log('Starting AsmDude LSP server...');
    lspProcess.stderr.on('data', (data) => {
        console.log(`LSP stderr: ${data}`);
    });

    // Give server time to start
    await new Promise(resolve => setTimeout(resolve, 1000));

    console.log('\nServer started. LSP protocol messages will be exchanged on stdin/stdout.');
    console.log('NOTE: This is a basic test setup.');
    console.log('For full LSP testing, use VS Code with an LSP client extension.\n');

    // Write sample .asm content to test
    const sampleAsm = `.intel_syntax noprefix
mov eax, ebx
call my_function
my_function:
    ret
`;

    console.log('Sample assembly code to test:');
    console.log(sampleAsm);
    console.log('\nTo test manually:');
    console.log('1. Open VS Code');
    console.log('2. Install "Assembly" or "MASM" extension');
    console.log('3. Create a .asm file with the code above');
    console.log('4. Hover over "mov" to see instruction details');
    console.log('5. Hover over "my_function" to see label info');
    console.log('6. Press F12 on "my_function" to go to definition');
}

testAsmDudeLSP().catch(console.error);
