// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

/// <summary>
/// Constants for the AsmDude2 Language Server.
/// IMPORTANT: Must match the values in AsmDude2LS.LanguageServerConstants
/// </summary>
internal static class LanguageServerConstants
{
    /// <summary>
    /// Assembly name of the LSP server (without extension).
    /// </summary>
    public const string AssemblyName = "AsmDude2.LSP";

    /// <summary>
    /// Executable name of the LSP server.
    /// </summary>
    public const string ExecutableName = AssemblyName + ".exe";
}
