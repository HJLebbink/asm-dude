// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace AsmDude.AsmDoc {

    #region Classification type/format exports

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "UnderlineClassification")]
    [Name("UnderlineClassificationFormat")]
    [UserVisible(true)]
    [Order(After = Priority.High)]
    internal sealed class UnderlineFormatDefinition : ClassificationFormatDefinition
    {
        public UnderlineFormatDefinition()
        {
            this.DisplayName = "AsmDude - Documentation Link";
            this.TextDecorations = System.Windows.TextDecorations.Underline;
            //this.ForegroundColor = Colors.Blue;
        }
    }

    #endregion

    #region Provider definition
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(ClassificationTag))]
    internal class UnderlineClassifierProvider : IViewTaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("UnderlineClassification")]
        internal static ClassificationTypeDefinition underlineClassificationType = null;

        static IClassificationType UnderlineClassification;
        public static UnderlineClassifier GetClassifierForView(ITextView view)
        {
            if (UnderlineClassification == null) {
                return null;
            }
            return view.Properties.GetOrCreateSingletonProperty(() => new UnderlineClassifier(view, UnderlineClassification));
        }

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (UnderlineClassification == null) {
                UnderlineClassification = ClassificationRegistry.GetClassificationType("UnderlineClassification");
            }
            if (textView.TextBuffer != buffer) {
                return null;
            }
            return GetClassifierForView(textView) as ITagger<T>;
        }
    }
    #endregion

    internal sealed class UnderlineClassifier : ITagger<ClassificationTag> {
        private readonly IClassificationType _classificationType;
        private readonly ITextView _textView;
        private SnapshotSpan? _underlineSpan;

        internal UnderlineClassifier(ITextView textView, IClassificationType classificationType) {
            _textView = textView;
            _classificationType = classificationType;
            _underlineSpan = null;
        }

        #region Private helpers

        void SendEvent(SnapshotSpan span) {
            var temp = this.TagsChanged;
            if (temp != null) {
                temp(this, new SnapshotSpanEventArgs(span));
            }
        }

        #endregion

        #region UnderlineClassification public members

        public SnapshotSpan? CurrentUnderlineSpan { get { return _underlineSpan; } }

        public void SetUnderlineSpan(SnapshotSpan? span) {
            var oldSpan = _underlineSpan;
            _underlineSpan = span;

            if (!oldSpan.HasValue && !_underlineSpan.HasValue) {
                return;
            } else if (oldSpan.HasValue && _underlineSpan.HasValue && oldSpan == _underlineSpan) {
                return;
            }
            if (!_underlineSpan.HasValue) {
                this.SendEvent(oldSpan.Value);
            } else {
                SnapshotSpan updateSpan = _underlineSpan.Value;
                if (oldSpan.HasValue) {
                    updateSpan = new SnapshotSpan(updateSpan.Snapshot,
                        Span.FromBounds(Math.Min(updateSpan.Start, oldSpan.Value.Start),
                                        Math.Max(updateSpan.End, oldSpan.Value.End)));
                }
                this.SendEvent(updateSpan);
            }
        }

        #endregion

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (!_underlineSpan.HasValue || (spans.Count == 0)) {
                yield break;
            }
            SnapshotSpan request = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End);
            SnapshotSpan underline = _underlineSpan.Value.TranslateTo(request.Snapshot, SpanTrackingMode.EdgeInclusive);
            if (underline.IntersectsWith(request)) { 
                yield return new TagSpan<ClassificationTag>(underline, new ClassificationTag(_classificationType));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}