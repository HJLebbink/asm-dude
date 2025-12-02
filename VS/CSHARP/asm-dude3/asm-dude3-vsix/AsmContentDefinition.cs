using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude3;

/// <summary>
/// Defines the content types for assembly language files
/// </summary>
public class AsmContentDefinition
{
    /// <summary>
    /// Content type definition for assembly files
    /// </summary>
    [Export]
    [Name("asm!")]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
#pragma warning disable CS0649
    internal static ContentTypeDefinition AsmContentTypeDefinition;
#pragma warning restore CS0649

    /// <summary>
    /// .asm file extension
    /// </summary>
    [Export]
    [FileExtension(".asm")]
    [ContentType(AsmDude3Package.AsmDudeContentType)]
#pragma warning disable CS0649
    internal static FileExtensionToContentTypeDefinition AsmFileExtensionDefinition;
#pragma warning restore CS0649

    /// <summary>
    /// .cod file extension (compiler output)
    /// </summary>
    [Export]
    [FileExtension(".cod")]
    [ContentType(AsmDude3Package.AsmDudeContentType)]
#pragma warning disable CS0649
    internal static FileExtensionToContentTypeDefinition CodFileExtensionDefinition;
#pragma warning restore CS0649

    /// <summary>
    /// .inc file extension (include files)
    /// </summary>
    [Export]
    [FileExtension(".inc")]
    [ContentType(AsmDude3Package.AsmDudeContentType)]
#pragma warning disable CS0649
    internal static FileExtensionToContentTypeDefinition IncFileExtensionDefinition;
#pragma warning restore CS0649

    /// <summary>
    /// .s file extension (GNU assembler)
    /// </summary>
    [Export]
    [FileExtension(".s")]
    [ContentType(AsmDude3Package.AsmDudeContentType)]
#pragma warning disable CS0649
    internal static FileExtensionToContentTypeDefinition SFileExtensionDefinition;
#pragma warning restore CS0649
}
