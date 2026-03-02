// Visual Studio specific LSP types and extensions
// Most VS types now come from Microsoft.VisualStudio.LanguageServer.Protocol.Extensions (18.5.1)
// This file contains only types not provided by the package

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS
{
    /// <summary>
    /// Visual Studio trace settings for LSP logging
    /// </summary>
    public enum TraceSetting
    {
        Off,
        Messages,
        Verbose
    }

    /// <summary>
    /// Additional CompletionItemKind values
    /// </summary>
    public static class CompletionItemKindExtensions
    {
        // Macro is not in LSP 3.16 spec, use Snippet as equivalent
        public const CompletionItemKind Macro = CompletionItemKind.Snippet;

        // None is not in LSP 3.16 spec, use Text as a fallback
        public const CompletionItemKind None = CompletionItemKind.Text;
    }
}
