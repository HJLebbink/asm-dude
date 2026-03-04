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

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AsmDude2LS.Tests;

/// <summary>
/// A test client that starts the LSP server as a real process and communicates
/// via JSON-RPC over stdin/stdout. This enables true integration testing of the
/// LSP protocol without requiring Visual Studio.
/// </summary>
public sealed class LspProcessTestClient : IAsyncDisposable
{
    private readonly Process _serverProcess;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly JsonSerializerOptions _jsonOptions;
    private int _requestId;
    private bool _initialized;
    private bool _disposed;

    private LspProcessTestClient(Process process)
    {
        _serverProcess = process;
        _writer = process.StandardInput;
        _reader = process.StandardOutput;
        _requestId = 0;
        _initialized = false;
        _disposed = false;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
    }

    /// <summary>
    /// Starts a new LSP server process and returns a connected client.
    /// </summary>
    /// <param name="serverProjectPath">Path to the asm-dude2-ls project directory</param>
    /// <param name="timeout">Timeout for server startup</param>
    public static async Task<LspProcessTestClient> StartAsync(
        string? serverProjectPath = null,
        TimeSpan? timeout = null)
    {
        // Find the server project path relative to the test project
        serverProjectPath ??= FindServerProjectPath();
        timeout ??= TimeSpan.FromSeconds(30);

        // Find the server executable based on the project path
        var serverDir = Path.GetDirectoryName(serverProjectPath)!;
        var exePath = Path.Combine(serverDir, "bin", "Release", "net10.0-windows", "AsmDude2.LSP.exe");

        // Fall back to Debug if Release doesn't exist
        if (!File.Exists(exePath))
        {
            exePath = Path.Combine(serverDir, "bin", "Debug", "net10.0-windows", "AsmDude2.LSP.exe");
        }

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"LSP server executable not found. Build the project first: {exePath}");
        }

        // Use UTF8 without BOM to avoid corrupting the LSP header
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--stdio",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = utf8NoBom,
            StandardOutputEncoding = utf8NoBom,
        };

        var process = new Process { StartInfo = startInfo };

        // Capture stderr for debugging
        var stderrBuilder = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start LSP server process");
        }

        process.BeginErrorReadLine();

        var client = new LspProcessTestClient(process);

        // Wait a moment for the server to be ready
        await Task.Delay(500);

        if (process.HasExited)
        {
            var stderr = stderrBuilder.ToString();
            throw new InvalidOperationException(
                $"LSP server process exited immediately with code {process.ExitCode}. Stderr: {stderr}");
        }

        return client;
    }

    private static string FindServerProjectPath()
    {
        // Navigate from test project to server project
        var currentDir = Directory.GetCurrentDirectory();

        // Try various relative paths
        var possiblePaths = new[]
        {
            Path.Combine(currentDir, "..", "..", "..", "..", "asm-dude2-ls", "asm-dude2-ls.csproj"),
            Path.Combine(currentDir, "..", "asm-dude2-ls", "asm-dude2-ls.csproj"),
            Path.Combine(currentDir, "asm-dude2-ls", "asm-dude2-ls.csproj"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Fallback: search upward for the solution
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            var serverPath = Path.Combine(dir.FullName, "VS", "CSHARP", "asm-dude2-ls", "asm-dude2-ls.csproj");
            if (File.Exists(serverPath))
            {
                return serverPath;
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "Could not find asm-dude2-ls.csproj. Please specify the path explicitly.");
    }

    /// <summary>
    /// Sends the initialize request and initialized notification.
    /// Must be called before any other LSP methods.
    /// </summary>
    public async Task<JsonNode?> InitializeAsync(string? rootUri = null)
    {
        if (_initialized)
            throw new InvalidOperationException("Already initialized");

        // Use a simple request format that works reliably
        var initParams = new
        {
            processId = Environment.ProcessId,
            rootUri = rootUri ?? "file:///test",
            capabilities = new { }
        };

        var response = await SendRequestAsync("initialize", initParams);

        // Send initialized notification
        await SendNotificationAsync("initialized", new { });

        _initialized = true;
        return response;
    }

    /// <summary>
    /// Opens a text document in the server.
    /// </summary>
    public async Task OpenDocumentAsync(string uri, string content, string languageId = "asm")
    {
        EnsureInitialized();

        var didOpenParams = new
        {
            textDocument = new
            {
                uri,
                languageId,
                version = 1,
                text = content
            }
        };

        await SendNotificationAsync("textDocument/didOpen", didOpenParams);

        // Give the server time to parse
        await Task.Delay(100);
    }

    /// <summary>
    /// Closes a text document.
    /// </summary>
    public async Task CloseDocumentAsync(string uri)
    {
        EnsureInitialized();

        var didCloseParams = new
        {
            textDocument = new { uri }
        };

        await SendNotificationAsync("textDocument/didClose", didCloseParams);
    }

    /// <summary>
    /// Requests hover information at a position.
    /// </summary>
    public async Task<JsonNode?> HoverAsync(string uri, int line, int character)
    {
        EnsureInitialized();

        var hoverParams = new
        {
            textDocument = new { uri },
            position = new { line, character }
        };

        return await SendRequestAsync("textDocument/hover", hoverParams);
    }

    /// <summary>
    /// Requests completion items at a position.
    /// </summary>
    public async Task<JsonNode?> CompletionAsync(string uri, int line, int character)
    {
        EnsureInitialized();

        var completionParams = new
        {
            textDocument = new { uri },
            position = new { line, character }
        };

        return await SendRequestAsync("textDocument/completion", completionParams);
    }

    /// <summary>
    /// Requests inlay hints for a range.
    /// </summary>
    public async Task<JsonNode?> InlayHintsAsync(string uri, int startLine, int startChar, int endLine, int endChar)
    {
        EnsureInitialized();

        var inlayHintParams = new
        {
            textDocument = new { uri },
            range = new
            {
                start = new { line = startLine, character = startChar },
                end = new { line = endLine, character = endChar }
            }
        };

        return await SendRequestAsync("textDocument/inlayHint", inlayHintParams);
    }

    /// <summary>
    /// Requests semantic tokens for a document.
    /// </summary>
    public async Task<JsonNode?> SemanticTokensAsync(string uri)
    {
        EnsureInitialized();

        var semanticTokensParams = new
        {
            textDocument = new { uri }
        };

        return await SendRequestAsync("textDocument/semanticTokens/full", semanticTokensParams);
    }

    /// <summary>
    /// Requests signature help at a position.
    /// </summary>
    public async Task<JsonNode?> SignatureHelpAsync(string uri, int line, int character)
    {
        EnsureInitialized();

        var signatureHelpParams = new
        {
            textDocument = new { uri },
            position = new { line, character }
        };

        return await SendRequestAsync("textDocument/signatureHelp", signatureHelpParams);
    }

    /// <summary>
    /// Requests definition locations for a position.
    /// </summary>
    public async Task<JsonNode?> DefinitionAsync(string uri, int line, int character)
    {
        EnsureInitialized();

        var definitionParams = new
        {
            textDocument = new { uri },
            position = new { line, character }
        };

        return await SendRequestAsync("textDocument/definition", definitionParams);
    }

    /// <summary>
    /// Requests references for a position.
    /// </summary>
    public async Task<JsonNode?> ReferencesAsync(string uri, int line, int character, bool includeDeclaration = true)
    {
        EnsureInitialized();

        var referencesParams = new
        {
            textDocument = new { uri },
            position = new { line, character },
            context = new { includeDeclaration }
        };

        return await SendRequestAsync("textDocument/references", referencesParams);
    }

    /// <summary>
    /// Requests folding ranges for a document.
    /// </summary>
    public async Task<JsonNode?> FoldingRangesAsync(string uri)
    {
        EnsureInitialized();

        var foldingRangeParams = new
        {
            textDocument = new { uri }
        };

        return await SendRequestAsync("textDocument/foldingRange", foldingRangeParams);
    }

    /// <summary>
    /// Sends shutdown request and exit notification.
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (!_initialized) return;

        await SendRequestAsync("shutdown", null);
        await SendNotificationAsync("exit", null);

        _initialized = false;
    }

    /// <summary>
    /// Sends a JSON-RPC request and waits for a response.
    /// Skips any notifications received while waiting for the response.
    /// </summary>
    public async Task<JsonNode?> SendRequestAsync(string method, object? @params, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        var id = Interlocked.Increment(ref _requestId);

        var request = new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params
        };

        await SendMessageAsync(request);

        // Read responses with timeout, skipping notifications until we get the actual response
        using var cts = new CancellationTokenSource(timeout.Value);

        while (!cts.Token.IsCancellationRequested)
        {
            var message = await ReadMessageAsync(cts.Token);

            if (message == null)
            {
                throw new TimeoutException($"No response received for {method} request");
            }

            // Check if this is a response (has "id" field) or a notification (has "method" field)
            var messageId = message["id"];
            if (messageId != null)
            {
                // This is a response - verify it matches our request id
                if (messageId.GetValue<int>() == id)
                {
                    // Check for error
                    var error = message["error"];
                    if (error != null)
                    {
                        throw new InvalidOperationException(
                            $"LSP error: {error["message"]?.GetValue<string>()} (code: {error["code"]?.GetValue<int>()})");
                    }

                    return message["result"];
                }
            }
            // If it's a notification (no id), just skip it and continue reading
        }

        throw new TimeoutException($"No response received for {method} request");
    }

    /// <summary>
    /// Sends a JSON-RPC notification (no response expected).
    /// </summary>
    public async Task SendNotificationAsync(string method, object? @params)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method,
            @params
        };

        await SendMessageAsync(notification);
    }

    private async Task SendMessageAsync(object message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var contentLength = Encoding.UTF8.GetByteCount(json);
        var header = $"Content-Length: {contentLength}\r\n\r\n";

        await _writer.WriteAsync(header);
        await _writer.WriteAsync(json);
        await _writer.FlushAsync();
    }

    private async Task<JsonNode?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        // Read headers
        int contentLength = -1;
        string? line;

        while ((line = await ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                break; // End of headers
            }

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var lengthStr = line["Content-Length:".Length..].Trim();
                contentLength = int.Parse(lengthStr);
            }
        }

        if (contentLength <= 0)
        {
            return null;
        }

        // Read content
        var buffer = new char[contentLength];
        var totalRead = 0;

        while (totalRead < contentLength)
        {
            var read = await _reader.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Server closed connection");
            }
            totalRead += read;
        }

        var json = new string(buffer);
        return JsonNode.Parse(json);
    }

    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_serverProcess.HasExited)
            return null;

        // Use ReadLineAsync(CancellationToken) which is properly cancellable on .NET 7+.
        // The old Peek()-based polling loop blocked because StreamReader.Peek() internally
        // calls Read() to fill its buffer, which blocks on Windows process pipes.
        return await _reader.ReadLineAsync(cancellationToken);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Client not initialized. Call InitializeAsync first.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_initialized)
            {
                await ShutdownAsync();
            }
        }
        catch
        {
            // Ignore errors during shutdown
        }

        try
        {
            // Cancel async stderr reading so the background thread exits cleanly.
            // Without this, BeginErrorReadLine's background thread keeps the test host alive.
            _serverProcess.CancelErrorRead();
        }
        catch { }

        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill();
            }
            // Always wait for the process to fully exit and its async I/O threads
            // to complete. Without this, the test host may hang after all tests finish.
            using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _serverProcess.WaitForExitAsync(exitCts.Token);
        }
        catch
        {
            // Ignore errors during kill/wait
        }

        _serverProcess.Dispose();
    }
}
