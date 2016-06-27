// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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
using System.Linq;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using System.Windows.Controls;
using AsmDude.Tools;
using AsmDude.SyntaxHighlighting;

namespace AsmDude.CodeFolding {

    class PartialRegion {
        public int StartLine { get; set; }
        public int StartOffset { get; set; }
        public int Level { get; set; }
        public PartialRegion PartialParent { get; set; }
    }

    class Region : PartialRegion {
        public int EndLine { get; set; }
    }

    internal sealed class CodeFoldingTagger : ITagger<IOutliningRegionTag> {

        #region Private Fields
        private string startRegionTag = Settings.Default.CodeFolding_BeginTag;  //the characters that start the outlining region
        private string endRegionTag = Settings.Default.CodeFolding_EndTag;      //the characters that end the outlining region

        private readonly ITextBuffer _buffer;
        private readonly IBufferTagAggregatorFactoryService _aggregatorFactory;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private ITextSnapshot _snapshot;
        private IList<Region> _regions;
        #endregion Private Fields

        /// <summary>Constructor</summary>
        public CodeFoldingTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory) {
            //Debug.WriteLine("INFO:OutliningTagger: constructor");
            this._buffer = buffer;
            this._aggregatorFactory = aggregatorFactory;
            this._aggregator = AsmDudeToolsStatic.getAggregator(buffer, this._aggregatorFactory);

            this._snapshot = buffer.CurrentSnapshot;
            this._regions = new List<Region>();
            this.ReParse();
            this._buffer.Changed += this.BufferChanged;
        }

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (spans.Count == 0) {
                yield break;
            }
            if (Settings.Default.CodeFolding_On) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: GetTags:entering: IsDefaultCollapsed={0}", Settings.Default.CodeFolding_IsDefaultCollapsed));

                SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(this._snapshot, SpanTrackingMode.EdgeExclusive);
                int startLineNumber = entire.Start.GetContainingLine().LineNumber;
                int endLineNumber = entire.End.GetContainingLine().LineNumber;

                foreach (Region region in this._regions) {
                    if ((region.StartLine <= endLineNumber) && (region.EndLine >= startLineNumber)) {

                        ITextSnapshotLine startLine = this._snapshot.GetLineFromLineNumber(region.StartLine);
                        ITextSnapshotLine endLine = this._snapshot.GetLineFromLineNumber(region.EndLine);

                        var replacement = this.getRegionDescription(startLine.GetText());
                        var hover = this.getHoverText(region.StartLine + 1, region.EndLine, this._snapshot);

                        yield return new TagSpan<IOutliningRegionTag>(
                            new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End),
                            new OutliningRegionTag(Settings.Default.CodeFolding_IsDefaultCollapsed, true, replacement, hover));
                    }
                }
            }
        }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #region Private Methods

        /// <summary>
        /// Get the description of the region that starts at the provided line content
        /// </summary>
        private string getRegionDescription(string line) {
            int startPos = 0;// line.IndexOf(startRegionTag[0]) + startRegionTag.Length + 1;
            if (startPos < line.Length) {
                string description = line.Substring(startPos).Trim();
                if (description.Length > 0) {
                    return description;
                }
            }
            return line.Trim();
        }

        /// <summary>
        /// Get the text to be displayed when hovering over a closed region
        /// </summary>
        private TextBlock getHoverText(int begin, int end, ITextSnapshot snapshot) {
            TextBlock description = new TextBlock();
            string str = "";
            if (begin < end) {
                str = snapshot.GetLineFromLineNumber(begin).GetText();
            }
            for (int i = begin + 1; i < end; ++i) {
                str += Environment.NewLine + snapshot.GetLineFromLineNumber(i).GetText();
            }

            //TODO provide syntax highlighting for the next run
            System.Windows.Documents.Run r = new System.Windows.Documents.Run(str);
            r.FontSize -= 1;
            description.Inlines.Add(r);
            return description;
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != _buffer.CurrentSnapshot) {
                return;
            }
            this.ReParse();
        }

        private int isStartRegion(string lineContent, int lineNumber) {
            int i1 = lineContent.IndexOf(startRegionTag, StringComparison.OrdinalIgnoreCase);
            if (i1 != -1) return i1;

            int i2 = lineContent.IndexOf("SEGMENT", StringComparison.OrdinalIgnoreCase);
            if (i2 != -1) {
                IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = this._aggregator.GetTags(this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in tags) {
                    if (asmTokenSpan.Tag.type == AsmTokenType.Directive) {
                        if (asmTokenSpan.Span.GetSpans(this._buffer)[0].GetText().Equals("SEGMENT", StringComparison.OrdinalIgnoreCase)) {
                            return i2;
                        }
                    }
                }
            }

            int i3 = lineContent.IndexOf("PROC", StringComparison.OrdinalIgnoreCase);
            if (i3 != -1) {
                IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = this._aggregator.GetTags(this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in tags) {
                    if (asmTokenSpan.Tag.type == AsmTokenType.Directive) {
                        if (asmTokenSpan.Span.GetSpans(this._buffer)[0].GetText().Equals("PROC", StringComparison.OrdinalIgnoreCase)) {
                            return i3;
                        }
                    }
                }
            }

            return -1;
        }

        private int isEndRegion(string lineContent) {
            int i1 = lineContent.IndexOf(endRegionTag, StringComparison.OrdinalIgnoreCase);
            if (i1 != -1) return i1;
            int i2 = lineContent.IndexOf("ENDS", StringComparison.OrdinalIgnoreCase);
            if (i2 != -1) return i2;
            int i3 = lineContent.IndexOf("ENDP", StringComparison.OrdinalIgnoreCase);
            if (i3 != -1) return i3;

            return -1;
        }

        private void ReParse() {
            //Debug.WriteLine("INFO:OutliningTagger:ReParse: entering");

            ITextSnapshot newSnapshot = _buffer.CurrentSnapshot;
            IList<Region> newRegions = new List<Region>();

            //keep the current (deepest) partial region, which will have
            // references to any parent partial regions.
            PartialRegion currentRegion = null;

            foreach (ITextSnapshotLine line in newSnapshot.Lines) {
                string text = line.GetText();

                //lines that contain a "[" denote the start of a new region.
                int regionStart = this.isStartRegion(text, line.LineNumber);
                if (regionStart != -1) {
                    int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
                    int newLevel;
                    if (!CodeFoldingTagger.TryGetLevel(text, regionStart, out newLevel)) {
                        newLevel = currentLevel + 1;
                    }

                    //levels are the same and we have an existing region;
                    //end the current region and start the next
                    if ((currentLevel == newLevel) && (currentRegion != null)) {
                        newRegions.Add(new Region() {
                            Level = currentRegion.Level,
                            StartLine = currentRegion.StartLine,
                            StartOffset = currentRegion.StartOffset,
                            EndLine = line.LineNumber
                        });

                        currentRegion = new PartialRegion() {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion.PartialParent
                        };
                    }
                    //this is a new (sub)region
                    else {
                        currentRegion = new PartialRegion() {
                            Level = newLevel,
                            StartLine = line.LineNumber,
                            StartOffset = regionStart,
                            PartialParent = currentRegion
                        };
                    }
                } else {
                    int regionEnd = this.isEndRegion(text);
                    //lines that contain "]" denote the end of a region
                    if (regionEnd != -1) {
                        int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
                        int closingLevel;
                        if (!CodeFoldingTagger.TryGetLevel(text, regionEnd, out closingLevel)) {
                            closingLevel = currentLevel;
                        }
                        //the regions match
                        if ((currentRegion != null) && (currentLevel == closingLevel)) {
                            newRegions.Add(new Region() {
                                Level = currentLevel,
                                StartLine = currentRegion.StartLine,
                                StartOffset = currentRegion.StartOffset,
                                EndLine = line.LineNumber
                            });

                            currentRegion = currentRegion.PartialParent;
                        }
                    }
                }
            }

            //determine the changed span, and send a changed event with the new spans
            IList<Span> oldSpans =
                new List<Span>(this._regions.Select(r => CodeFoldingTagger.AsSnapshotSpan(r, this._snapshot)
                    .TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive)
                    .Span));
            IList<Span> newSpans = new List<Span>(newRegions.Select(r => CodeFoldingTagger.AsSnapshotSpan(r, newSnapshot).Span));

            NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
            NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);

            //the changed regions are regions that appear in one set or the other, but not both.
            NormalizedSpanCollection removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

            int changeStart = int.MaxValue;
            int changeEnd = -1;

            if (removed.Count > 0) {
                changeStart = removed[0].Start;
                changeEnd = removed[removed.Count - 1].End;
            }

            if (newSpans.Count > 0) {
                changeStart = Math.Min(changeStart, newSpans[0].Start);
                changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
            }

            this._snapshot = newSnapshot;
            this._regions = newRegions;

            if (changeStart <= changeEnd) {
                ITextSnapshot snap = this._snapshot;
                if (this.TagsChanged != null) {
                    this.TagsChanged(this, new SnapshotSpanEventArgs(
                        new SnapshotSpan(this._snapshot, Span.FromBounds(changeStart, changeEnd))));
                }
            }
            //Debug.WriteLine("INFO:OutliningTagger:ReParse: leaving");
        }

        private static bool TryGetLevel(string text, int startIndex, out int level) {
            level = -1;
            if (text.Length > startIndex + 3) {
                if (int.TryParse(text.Substring(startIndex + 1), out level)) {
                    return true;
                }
            }
            return false;
        }

        private static SnapshotSpan AsSnapshotSpan(Region region, ITextSnapshot snapshot) {
            var startLine = snapshot.GetLineFromLineNumber(region.StartLine);
            var endLine = (region.StartLine == region.EndLine) ? startLine : snapshot.GetLineFromLineNumber(region.EndLine);
            return new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
        }

        #endregion Private Methods
    }
}
