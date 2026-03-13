// Visual Studio specific LSP types and extensions
// Most VS types now come from Microsoft.VisualStudio.LanguageServer.Protocol.Extensions (18.5.1)
// This file contains only types not provided by the package

namespace AsmDude2LS;

/// <summary>
/// Visual Studio trace settings for LSP logging
/// </summary>
public enum TraceSetting
{
    Off,
    Messages,
    Verbose
}
