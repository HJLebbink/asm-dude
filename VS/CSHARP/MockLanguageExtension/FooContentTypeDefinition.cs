using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace MockLanguageExtension
{
#pragma warning disable 649
    public class FooContentDefinition
    {
        [Export]
        [Name("foo")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition FooContentTypeDefinition;


        [Export]
        [FileExtension(".foo")]
        [ContentType("foo")]
        internal static FileExtensionToContentTypeDefinition FooFileExtensionDefinition;
    }
#pragma warning restore 649
}
