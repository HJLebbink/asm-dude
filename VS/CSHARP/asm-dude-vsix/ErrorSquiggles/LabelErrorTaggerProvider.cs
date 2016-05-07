using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Shell;

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
        private AsmDudeTools _asmDudeTools = null;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {

            Func<ITagger<T>> sc = delegate () {
                ITagAggregator<AsmTokenTag> asmTagAggregator = _aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
                return new LabelErrorTagger(buffer, asmTagAggregator, _asmDudeTools.errorListProvider) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }
    }
}
