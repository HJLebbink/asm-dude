using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

using System.ComponentModel.Composition;

namespace AsmDude2
{
    public class AsmContentDefinition
    {
        [Export]
        [Name("asm")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition BarContentTypeDefinition;

        [Export]
        [FileExtension(".asm")]
        [ContentType("asm!")]
        internal static FileExtensionToContentTypeDefinition BarFileExtensionDefinition;
    }
}
