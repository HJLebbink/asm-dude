using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/*
namespace AsmDude.ErrorSquiggles {

    /// <summary>Boilerplate factory class that associates <see cref="SampleLanguageForVS"/>,
    /// and file extension .samplelang, with content type "Sample Language".</summary>
    [Export(typeof(IClassifierProvider))]
    [Export(typeof(ITaggerProvider))]

    [TagType(typeof(ClassificationTag))]
    [TagType(typeof(ErrorTag))]
    [ContentType("Sample Language")]
    internal class SampleLanguageForVSProvider : IClassifierProvider, ITaggerProvider {
        [Export]
        [Name("Sample Language")] // Must match the [ContentType] attributes
        [BaseDefinition("code")]
        internal static ContentTypeDefinition _ = null;
        [Export]
        [FileExtension(".samplelang")]
        [ContentType("Sample Language")]
        internal static FileExtensionToContentTypeDefinition _1 = null;

        [Import]
        IClassificationTypeRegistryService _registry = null; // Set via MEF

        public static SampleLanguageForVS Get(IClassificationTypeRegistryService registry, ITextBuffer buffer) {
            return buffer.Properties.GetOrCreateSingletonProperty<SampleLanguageForVS>(
                delegate { return new SampleLanguageForVS(registry, buffer); });
        }
        public IClassifier GetClassifier(ITextBuffer buffer) {
            return Get(_registry, buffer);
        }
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            return Get(_registry, buffer) as ITagger<T>;
        }
    }

    internal class SampleLanguageForVS : IClassifier,
        ITagger<ClassificationTag>,
        ITagger<ErrorTag>,
        IBackgroundAnalyzerImpl<object, IList<ITagSpan<ITag>>> {
        protected IClassificationTypeRegistryService _registry;
        protected ITextBuffer _buffer;
        protected IClassificationType _commentType;
        protected ClassificationTag _outerParenTag;
        protected IList<ITagSpan<ITag>> _resultTags;
        protected BackgroundAnalyzerForVS<object, IList<ITagSpan<ITag>>> _parseHelper;

        public SampleLanguageForVS(IClassificationTypeRegistryService registry, ITextBuffer buffer) {
            _registry = registry;
            _buffer = buffer;
            _commentType = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            _outerParenTag = MakeTag(PredefinedClassificationTypeNames.Keyword);
            _parseHelper = new BackgroundAnalyzerForVS<object, IList<ITagSpan<ITag>>>(buffer, this, true);
        }
        ClassificationTag MakeTag(string name) {
            return new ClassificationTag(_registry.GetClassificationType(name));
        }

        #region Classifier (lexical analysis)

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            List<ClassificationSpan> spans = new List<ClassificationSpan>();
            var line = span.Snapshot.GetLineFromPosition(span.Start);
            do {
                var cspan = GetLineClassification(line);
                if (cspan != null)
                    spans.Add(cspan);

                if (line.EndIncludingLineBreak.Position >= span.Snapshot.Length) break;
                line = span.Snapshot.GetLineFromPosition(line.EndIncludingLineBreak.Position);
            } while (line.EndIncludingLineBreak < span.End.Position);
            return spans;
        }

        public ClassificationSpan GetLineClassification(ITextSnapshotLine line) {
            var span = new Span(line.Start.Position, line.Length);
            var sspan = new SnapshotSpan(line.Snapshot, span);
            int i;
            for (i = span.Start; i < line.Snapshot.Length && char.IsWhiteSpace(line.Snapshot[i]); i++) { }
            if (i < line.Snapshot.Length &&
                (line.Snapshot[i] == '#' ||
                 line.Snapshot[i] == '/' && i + 1 < line.Snapshot.Length && line.Snapshot[i + 1] == '/'))
                return new ClassificationSpan(sspan, _commentType);
            return null;
        }

        #endregion

        #region Background analysis (the two taggers)

        public object GetInputSnapshot() {
            return null; // this example has no state to pass to the analysis thread.
        }
        public IList<ITagSpan<ITag>> RunAnalysis(ITextSnapshot snapshot, object input, System.Threading.CancellationToken cancelToken) {
            List<ITagSpan<ITag>> results = new List<ITagSpan<ITag>>();
            // On analysis thread: produce classification tags for nested [(parens)]
            // and warning tags for backslashes.
            int parenLevel = 0;
            for (int i = 0; i < snapshot.Length; i++) {
                char c = snapshot[i];
                if (c == '\\')
                    results.Add(new TagSpan<ErrorTag>(
                        new SnapshotSpan(snapshot, new Span(i, 1)),
                        new ErrorTag("compiler warning", "Caution: that's not really a slash, it's a backslash!!")));
                bool open = (c == '[' || c == '(');
                bool close = (c == ']' || c == ')');
                if (close) {
                    if (parenLevel > 0)
                        parenLevel--;
                    else {
                        results.Add(new TagSpan<ErrorTag>(
                            new SnapshotSpan(snapshot, new Span(i, Math.Min(2, snapshot.Length - i))),
                            new ErrorTag("syntax error", "Caution: closing parenthesis without matching opener")));
                    }
                }
                if ((open || close) && parenLevel == 0)
                    results.Add(new TagSpan<ClassificationTag>(
                        new SnapshotSpan(snapshot, new Span(i, 1)),
                        _outerParenTag));
                if (open)
                    parenLevel++;
            }
            return results;
        }
        public void OnRunSucceeded(IList<ITagSpan<ITag>> results) {
            _resultTags = results;
            // We don't know which tags changed unless we do some fancy diff, so
            // act as if everything changed.
            if (TagsChanged != null) // should always be true
                TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, new Span(0, _buffer.CurrentSnapshot.Length))));
        }

        #endregion

        #region ITagger<ClassificationTag> and ITagger<ErrorTag> Members

        IEnumerable<ITagSpan<ErrorTag>> ITagger<ErrorTag>.GetTags(NormalizedSnapshotSpanCollection spans) {
            return GetTags<ErrorTag>(spans);
        }
        IEnumerable<ITagSpan<ClassificationTag>> ITagger<ClassificationTag>.GetTags(NormalizedSnapshotSpanCollection spans) {
            return GetTags<ClassificationTag>(spans);
        }
        public IEnumerable<ITagSpan<TTag>> GetTags<TTag>(NormalizedSnapshotSpanCollection spans) where TTag : ITag {
            if (_resultTags == null)
                return null;

            // TODO: make more efficient for large files with e.g. binary search
            int start = spans[0].Start.Position, end = spans[spans.Count - 1].End.Position;
            return _resultTags.Where(ts => ts.Span.End >= start && ts.Span.Start <= end).OfType<ITagSpan<TTag>>();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion
    }
}
    */
