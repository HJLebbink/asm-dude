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
            Func<ITagAggregator<AsmTokenTag>> sc2 = delegate () {
                return _aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
            };
            Func<ILabelGraph> sc1 = delegate () {
                IContentType contentType = this._contentService.GetContentType(AsmDudePackage.AsmDudeContentType);
                return AsmDudeToolsStatic.createLabelGraph(buffer, _aggregatorFactory, _docFactory, contentType);
            };
            Func<ITagger<T>> sc = delegate () {
                ITagAggregator<AsmTokenTag> aggregator = buffer.Properties.GetOrCreateSingletonProperty(sc2);
                ILabelGraph labelGraph = buffer.Properties.GetOrCreateSingletonProperty(sc1);
                return new LabelErrorTagger(buffer, aggregator, labelGraph, labelGraph.errorListProvider) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
