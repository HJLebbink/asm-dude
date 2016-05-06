using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace AsmDude.SyntaxHighlighting {

    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TagType(typeof(ClassificationTag))]
    internal sealed class AsmClassifierProvider : ITaggerProvider {

        [Export]
        [Name("asm!")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition AsmContentType = null;

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType = null;

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_cod = null;

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_inc = null;

        [Import]
        private IClassificationTypeRegistryService _classificationTypeRegistry = null;

        [Import]
        private IBufferTagAggregatorFactoryService _aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            Func<ITagger<T>> sc = delegate () {
                ITagAggregator<AsmTokenTag> asmTagAggregator = _aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
                return new AsmClassifier(buffer, asmTagAggregator, _classificationTypeRegistry) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }
    }
}
