using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude2
{
    public class FooContentDefinition
    {
        [Export]
        [Name("foo")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition AsmContentType;

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType = null;

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_cod = null;

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_inc = null;

        [Export]
        [FileExtension(".s")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_s = null;
    }
}
