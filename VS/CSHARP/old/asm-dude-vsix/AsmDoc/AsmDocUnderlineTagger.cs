// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude.AsmDoc
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class AsmDocUnderlineTagger : ITagger<ClassificationTag>
    {
        private readonly IClassificationType classificationType_;
        private readonly ITextView textView_;
        private SnapshotSpan? underlineSpan_;

        internal AsmDocUnderlineTagger(ITextView textView, IClassificationType classificationType)
        {
            this.textView_ = textView ?? throw new ArgumentNullException(nameof(textView));
            this.classificationType_ = classificationType ?? throw new ArgumentNullException(nameof(classificationType));
            this.underlineSpan_ = null;
        }

        #region UnderlineClassification public members

        public SnapshotSpan? CurrentUnderlineSpan { get { return this.underlineSpan_; } }

        public void SetUnderlineSpan(SnapshotSpan? span)
        {
            SnapshotSpan? oldSpan = this.underlineSpan_;
            this.underlineSpan_ = span;

            if (!oldSpan.HasValue && !this.underlineSpan_.HasValue)
            {
                return;
            }
            else if (oldSpan.HasValue && this.underlineSpan_.HasValue && oldSpan == this.underlineSpan_)
            {
                return;
            }
            if (!this.underlineSpan_.HasValue)
            {
                this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(oldSpan.Value));
            }
            else
            {
                SnapshotSpan updateSpan = this.underlineSpan_.Value;
                if (oldSpan.HasValue)
                {
                    updateSpan = new SnapshotSpan(
                        updateSpan.Snapshot,
                        Span.FromBounds(
                            Math.Min(updateSpan.Start, oldSpan.Value.Start),
                            Math.Max(updateSpan.End, oldSpan.Value.End)));
                }
                this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(updateSpan));
            }
        }

        #endregion

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!this.underlineSpan_.HasValue || (spans.Count == 0))
            {
                yield break;
            }
            SnapshotSpan request = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End);
            SnapshotSpan underline = this.underlineSpan_.Value.TranslateTo(request.Snapshot, SpanTrackingMode.EdgeInclusive);
            if (underline.IntersectsWith(request))
            {
                yield return new TagSpan<ClassificationTag>(underline, new ClassificationTag(this.classificationType_));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
