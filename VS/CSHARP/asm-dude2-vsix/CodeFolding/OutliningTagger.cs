// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmDude2.CodeFolding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal class PartialRegion
    {
        public int StartLine { get; set; }

        public int StartOffset { get; set; }

        public int StartOffsetHoverText { get; set; }

        public PartialRegion PartialParent { get; set; }
    }

    internal class Region : PartialRegion
    {
        public int EndLine { get; set; }
    }

    internal sealed class OutliningTagger : ITagger<IOutliningRegionTag>
    {
        private readonly ITextBuffer buffer_;
        private ITextSnapshot snapshot_;
        private IList<Region> regions_;
        private readonly object updateLock_ = new object();

        public OutliningTagger(ITextBuffer buffer)
        {
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.snapshot_ = buffer.CurrentSnapshot;
            this.regions_ = new List<Region>();

            Parse();
            this.buffer_.ChangedLowPriority += Buffer_Changed;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            if (!Settings.Default.CodeFolding_On)
            {
                yield break;
            }

            lock (this.updateLock_)
            {
                SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End)
                    .TranslateTo(this.snapshot_, SpanTrackingMode.EdgeExclusive);
                int startLineNumber = entire.Start.GetContainingLine().LineNumber;
                int endLineNumber = entire.End.GetContainingLine().LineNumber;

                foreach (Region region in this.regions_)
                {
                    if (region.StartLine <= endLineNumber && region.EndLine >= startLineNumber)
                    {
                        ITextSnapshotLine startLine = this.snapshot_.GetLineFromLineNumber(region.StartLine);
                        ITextSnapshotLine endLine = this.snapshot_.GetLineFromLineNumber(region.EndLine);

                        string collapsedForm = GetRegionDescription(startLine.GetText(), region.StartOffsetHoverText);
                        string collapsedHintForm = GetHoverText(region.StartLine, region.EndLine, this.snapshot_);

                        yield return new TagSpan<IOutliningRegionTag>(
                            new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End),
                            new OutliningRegionTag(false, true, collapsedForm, collapsedHintForm));
                    }
                }
            }
        }

        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            Parse();
        }

        private void Parse()
        {
            lock (this.updateLock_)
            {
                string beginTag = Settings.Default.CodeFolding_BeginTag;
                string endTag = Settings.Default.CodeFolding_EndTag;

                if (string.IsNullOrEmpty(beginTag) || string.IsNullOrEmpty(endTag))
                {
                    return;
                }

                ITextSnapshot newSnapshot = this.buffer_.CurrentSnapshot;
                IList<Region> newRegions = new List<Region>();
                PartialRegion currentRegion = null;

                foreach (ITextSnapshotLine line in newSnapshot.Lines)
                {
                    string lineContent = line.GetText();
                    int lineNumber = line.LineNumber;

                    int regionStart = lineContent.IndexOf(beginTag, StringComparison.OrdinalIgnoreCase);
                    if (regionStart != -1)
                    {
                        int descriptionStart = regionStart + beginTag.Length;
                        currentRegion = new PartialRegion
                        {
                            StartLine = lineNumber,
                            StartOffset = regionStart,
                            StartOffsetHoverText = descriptionStart,
                            PartialParent = currentRegion,
                        };
                    }
                    else
                    {
                        int regionEnd = lineContent.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
                        if (regionEnd != -1 && currentRegion != null)
                        {
                            newRegions.Add(new Region
                            {
                                StartLine = currentRegion.StartLine,
                                StartOffset = currentRegion.StartOffset,
                                StartOffsetHoverText = currentRegion.StartOffsetHoverText,
                                EndLine = lineNumber,
                            });
                            currentRegion = currentRegion.PartialParent;
                        }
                    }
                }

                UpdateChangedSpans(newSnapshot, newRegions);
            }
        }

        private void UpdateChangedSpans(ITextSnapshot newSnapshot, IList<Region> newRegions)
        {
            IList<Span> oldSpans = new List<Span>(
                this.regions_.Select(r => AsSnapshotSpan(r, this.snapshot_)
                    .TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive)
                    .Span));
            IList<Span> newSpans = new List<Span>(
                newRegions.Select(r => AsSnapshotSpan(r, newSnapshot).Span));

            NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
            NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);
            NormalizedSpanCollection removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart = int.MaxValue;
            int changeEnd = -1;

            if (removed.Count > 0)
            {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }
            if (newSpans.Count > 0)
            {
                changeStart = Math.Min(changeStart, newSpans[0].Start);
                changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
            }

            this.snapshot_ = newSnapshot;
            this.regions_ = newRegions;

            if (changeStart <= changeEnd)
            {
                this.TagsChanged?.Invoke(this,
                    new SnapshotSpanEventArgs(new SnapshotSpan(this.snapshot_, Span.FromBounds(changeStart, changeEnd))));
            }
        }

        private static SnapshotSpan AsSnapshotSpan(Region region, ITextSnapshot snapshot)
        {
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(region.StartLine);
            ITextSnapshotLine endLine = (region.StartLine == region.EndLine)
                ? startLine
                : snapshot.GetLineFromLineNumber(region.EndLine);
            return new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
        }

        private static string GetRegionDescription(string line, int startPos)
        {
            string description = string.Empty;
            if (startPos < 0)
            {
                description = line;
            }
            else if (startPos < line.Length)
            {
                description = line.Substring(startPos).Trim();
            }
            return description.Length > 0 ? description : "...";
        }

        private static string GetHoverText(int beginLineNumber, int endLineNumber, ITextSnapshot snapshot)
        {
            StringBuilder sb = new StringBuilder();
            int numberOfLines = Math.Min(endLineNumber + 1 - beginLineNumber, 40);
            for (int i = 0; i < numberOfLines; ++i)
            {
                sb.AppendLine(snapshot.GetLineFromLineNumber(beginLineNumber + i).GetText());
            }
            return sb.ToString().TrimEnd();
        }
    }
}
