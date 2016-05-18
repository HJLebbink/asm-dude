using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;

namespace AsmDude.ErrorSquiggles {

    /// <summary>
    /// Export a <see cref="IViewTaggerProvider"/>
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TagType(typeof(ErrorTag))]
    internal sealed class LabelErrorTaggerProvider : IViewTaggerProvider {

        [Import]
        private IBufferTagAggregatorFactoryService _aggregatorFactory = null;

        [Import]
        private ITextDocumentFactoryService _docFactory = null;

        [Import]
        private IContentTypeRegistryService _contentService = null;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

            Func<ITagger<T>> sc = delegate () {
                ITagAggregator<AsmTokenTag> aggregator = AsmDudeToolsStatic.getAggregator(buffer, _aggregatorFactory);
                ILabelGraph labelGraph = AsmDudeToolsStatic.getLabelGraph(buffer, _aggregatorFactory, _docFactory, _contentService);
                return new LabelErrorTagger(buffer, aggregator, labelGraph, labelGraph.errorListProvider) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
