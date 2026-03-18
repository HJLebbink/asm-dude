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

namespace AsmDude2;

using System.Diagnostics;
using System.IO.Pipelines;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Extensibility.LanguageServer;

[VisualStudioContribution]
internal class AsmLanguageServerProvider : LanguageServerProvider
{
    [VisualStudioContribution]
    internal static DocumentTypeConfiguration AsmDocumentType => new("asm")
    {
        FileExtensions = [".asm"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration CodDocumentType => new("cod")
    {
        FileExtensions = [".cod"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration IncDocumentType => new("inc")
    {
        FileExtensions = [".inc"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    [VisualStudioContribution]
    internal static DocumentTypeConfiguration SDocumentType => new("s")
    {
        FileExtensions = [".s"],
        BaseDocumentType = LanguageServerBaseDocumentType,
    };

    public override LanguageServerProviderConfiguration LanguageServerProviderConfiguration =>
        new("AsmDude2 Language Server",
        [
            DocumentFilter.FromDocumentType(AsmDocumentType),
            DocumentFilter.FromDocumentType(CodDocumentType),
            DocumentFilter.FromDocumentType(IncDocumentType),
            DocumentFilter.FromDocumentType(SDocumentType),
        ]);

    public override Task<IDuplexPipe?> CreateServerConnectionAsync(
        CancellationToken cancellationToken)
    {
        string? extensionDir = Path.GetDirectoryName(
            typeof(AsmLanguageServerProvider).Assembly.Location);
        if (extensionDir is null)
            return Task.FromResult<IDuplexPipe?>(null);

        string lspPath = Path.Combine(extensionDir, "Server", "AsmDude2.LSP.exe");
        if (!File.Exists(lspPath))
            return Task.FromResult<IDuplexPipe?>(null);

        var info = new ProcessStartInfo
        {
            FileName = lspPath,
            Arguments = "--stdio",
            WorkingDirectory = Path.GetDirectoryName(lspPath),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = info };
        if (process.Start())
        {
            return Task.FromResult<IDuplexPipe?>(new DuplexPipe(
                PipeReader.Create(process.StandardOutput.BaseStream),
                PipeWriter.Create(process.StandardInput.BaseStream)));
        }

        return Task.FromResult<IDuplexPipe?>(null);
    }

    private sealed class DuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;
        public PipeWriter Output { get; } = output;
    }
}
