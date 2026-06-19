// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2;

/// <summary>
/// A label CodeLens record computed by the LSP server (definition position + reference count),
/// rendered as-is by the VSIX tagger. Reference counting lives only on the server.
/// </summary>
internal sealed record AsmLabelRef(string Label, int DefinitionLine, int DefinitionColumn, int DefinitionLength, int ReferenceCount);
