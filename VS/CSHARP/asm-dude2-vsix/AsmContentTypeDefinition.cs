using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude2
{
    public class AsmContentDefinition
    {
        [Export]
        [Name("asm!")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition asmContentTypeDefinition;

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition;

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_cod;

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_inc;

        [Export]
        [FileExtension(".s")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_s;
    }
}
