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
using System.Threading;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace AsmDude.CodeFolding {

    class PartialRegion {
        public int StartLine { get; set; }
        public int StartOffset { get; set; }
        public int StartOffsetHoverText { get; set; }
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
        private readonly ErrorListProvider _errorListProvider;
        private ITextSnapshot _snapshot;
        private IList<Region> _regions;
        private object _updateLock = new object();
        private bool _enabled;

        private bool _busy;
        private bool _waiting;
        private bool _scheduled;


        #endregion Private Fields

        /// <summary>Constructor</summary>
        public CodeFoldingTagger(
            ITextBuffer buffer,
            ITagAggregator<AsmTokenTag> aggregator,
            ErrorListProvider errorListProvider) {

            //Debug.WriteLine("INFO:OutliningTagger: constructor");
            this._buffer = buffer;
            this._aggregator = aggregator;
            this._errorListProvider = errorListProvider;

            this._snapshot = buffer.CurrentSnapshot;
            this._regions = new List<Region>();
            this._enabled = true;
            this._busy = false;
            this._waiting = false;
            this._scheduled = false;

            this.parse_Delayed();
            this._buffer.ChangedLowPriority += this.BufferChanged;
        }

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (spans.Count == 0) {
                yield break;
            }
            if (Settings.Default.CodeFolding_On && this._enabled) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: GetTags:entering: IsDefaultCollapsed={0}", Settings.Default.CodeFolding_IsDefaultCollapsed));

                lock (_updateLock) {
                    SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(this._snapshot, SpanTrackingMode.EdgeExclusive);
                    int startLineNumber = entire.Start.GetContainingLine().LineNumber;
                    int endLineNumber = entire.End.GetContainingLine().LineNumber;

                    Region[] regionArray = this._regions.ToArray();//TODO expensive and ugly ToList here to prevent a modification exception
                    foreach (Region region in regionArray) {
                        if ((region.StartLine <= endLineNumber) && (region.EndLine >= startLineNumber)) {

                            ITextSnapshotLine startLine = this._snapshot.GetLineFromLineNumber(region.StartLine);
                            ITextSnapshotLine endLine = this._snapshot.GetLineFromLineNumber(region.EndLine);

                            var replacement = this.getRegionDescription(startLine.GetText(), region.StartOffsetHoverText);
                            var hover = this.getHoverText(region.StartLine, region.EndLine, this._snapshot);

                            yield return new TagSpan<IOutliningRegionTag>(
                                new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End),
                                new OutliningRegionTag(Settings.Default.CodeFolding_IsDefaultCollapsed, true, replacement, hover));
                        }
                    }
                }
            }
        }
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public bool isEnabled { get { return this._enabled; } }

        #region Private Methods

        /// <summary>
        /// Get the description of the region that starts at the provided line content
        /// </summary>
        private string getRegionDescription(string line, int startPos) {
            string description = "";
            //AsmDudeToolsStatic.Output("getRegionDescription: startPos=" + startPos + "; line=" + line);
            if (startPos < line.Length) {
                description = line.Substring(startPos).Trim();
            }
            return (description.Length > 0) ? description : "...";
        }

        /// <summary>
        /// Get the text to be displayed when hovering over a closed region
        /// </summary>
        private TextBlock getHoverText(int beginLineNumber, int endLineNumber, ITextSnapshot snapshot) {
            TextBlock description = new TextBlock();
            StringBuilder str = new StringBuilder();

            int numberOfLines = Math.Min(endLineNumber - beginLineNumber, 40); // do not show more than 40 lines 
            for (int i = 0; i < numberOfLines; ++i) {
                str.AppendLine(snapshot.GetLineFromLineNumber(beginLineNumber + i).GetText());
            }

            //TODO provide syntax highlighting for the next run
            System.Windows.Documents.Run run = new System.Windows.Documents.Run(str.ToString().TrimEnd()); // TrimEnd to get rid of the last new line
            run.FontSize -= 1;
            description.Inlines.Add(run);
            return description;
        }

        private void BufferChanged(object sender, TextContentChangedEventArgs e) {
            // If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
            if (e.After != _buffer.CurrentSnapshot) {
                return;
            }
            this.parse_Delayed();
        }

        /// <summary>
        /// Return start positions of the provided line content, tuple has start folding position and start of description position
        /// </summary>
        private Tuple<int, int> isStartRegion(string lineContent, int lineNumber) {
            int i1 = lineContent.IndexOf(startRegionTag, StringComparison.OrdinalIgnoreCase);
            if (i1 != -1) return new Tuple<int, int>(i1, i1 + startRegionTag.Length);

            IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = this._aggregator.GetTags(this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in tags) {
                if (asmTokenSpan.Tag.type == AsmTokenType.Directive) {
                    string tokenStr = asmTokenSpan.Span.GetSpans(this._buffer)[0].GetText();
                    if (tokenStr.Equals("SEGMENT", StringComparison.OrdinalIgnoreCase)) {
                        //return lineContent.IndexOf("SEGMENT", StringComparison.OrdinalIgnoreCase);
                        return new Tuple<int, int>(lineContent.Length, lineContent.Length);
                    }
                    if (tokenStr.Equals("PROC", StringComparison.OrdinalIgnoreCase)) {
                        //return lineContent.IndexOf("PROC", StringComparison.OrdinalIgnoreCase);
                        return new Tuple<int, int>(lineContent.Length, lineContent.Length);
                    }
                    if (tokenStr.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                        //return lineContent.IndexOf("MACRO", StringComparison.OrdinalIgnoreCase);
                        return new Tuple<int, int>(lineContent.Length, lineContent.Length);
                    }
                }
            }
            return new Tuple<int, int>(-1, -1);
        }

        private int isEndRegion(string lineContent, int lineNumber) {
            int i1 = lineContent.IndexOf(endRegionTag, StringComparison.OrdinalIgnoreCase);
            if (i1 != -1) return i1;

            IEnumerable<IMappingTagSpan<AsmTokenTag>> tags = this._aggregator.GetTags(this._buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).Extent);
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in tags) {
                if (asmTokenSpan.Tag.type == AsmTokenType.Directive) {
                    string tokenStr = asmTokenSpan.Span.GetSpans(this._buffer)[0].GetText();
                    if (tokenStr.Equals("ENDS", StringComparison.OrdinalIgnoreCase)) {
                        return lineContent.IndexOf("ENDS", StringComparison.OrdinalIgnoreCase);
                    }
                    if (tokenStr.Equals("ENDP", StringComparison.OrdinalIgnoreCase)) {
                        return lineContent.IndexOf("ENDP", StringComparison.OrdinalIgnoreCase);
                    }
                    if (tokenStr.Equals("ENDM", StringComparison.OrdinalIgnoreCase)) {
                        return lineContent.IndexOf("ENDM", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return -1;
        }

        private void parse_Delayed() {
            if (this._waiting) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: CodeFoldingTagger:reparse_delayed: already waiting for execution. Skipping this call."));
                return;
            }
            if (this._busy) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: CodeFoldingTagger:reparse_delayed: busy; scheduling this call."));
                this._scheduled = true;
            } else {
                //AsmDudeToolsStatic.Output(string.Format("INFO: CodeFoldingTagger:reparse_delayed: going to execute this call."));
                ThreadPool.QueueUserWorkItem(this.parse);
            }
        }

        private void parse(object threadContext) {
            if (!this._enabled) return;

            this._waiting = true;
            Thread.Sleep(AsmDudePackage.msSleepBeforeAsyncExecution);
            this._busy = true;
            this._waiting = false;

            #region Payload
            lock (_updateLock) {
                DateTime time1 = DateTime.Now;

                ITextSnapshot newSnapshot = _buffer.CurrentSnapshot;
                IList<Region> newRegions = new List<Region>();

                // keep the current (deepest) partial region, which will have
                // references to any parent partial regions.
                PartialRegion currentRegion = null;

                foreach (ITextSnapshotLine line in newSnapshot.Lines) {
                    #region Parse Line
                    string text = line.GetText();

                    Tuple<int, int> tup = this.isStartRegion(text, line.LineNumber);
                    int regionStart = tup.Item1;
                    int regionStartHoverText = tup.Item2;
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
                                StartOffsetHoverText = regionStartHoverText,
                                EndLine = line.LineNumber
                            });

                            currentRegion = new PartialRegion() {
                                Level = newLevel,
                                StartLine = line.LineNumber,
                                StartOffset = regionStart,
                                StartOffsetHoverText = regionStartHoverText,
                                PartialParent = currentRegion.PartialParent
                            };
                        }
                        //this is a new (sub)region
                        else {
                            currentRegion = new PartialRegion() {
                                Level = newLevel,
                                StartLine = line.LineNumber,
                                StartOffset = regionStart,
                                StartOffsetHoverText = regionStartHoverText,
                                PartialParent = currentRegion
                            };
                        }
                    } else {
                        int regionEnd = this.isEndRegion(text, line.LineNumber);
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
                                    StartOffsetHoverText = currentRegion.StartOffsetHoverText,
                                    EndLine = line.LineNumber
                                });

                                currentRegion = currentRegion.PartialParent;
                            }
                        }
                    }
                    #endregion Parse Line

                    #region Update Changed Spans
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
                        if (this.TagsChanged != null) {
                            this.TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(this._snapshot, Span.FromBounds(changeStart, changeEnd))));
                        } else {
                            AsmDudeToolsStatic.Output("reparse_Sync: TagsChanged is null");
                        }
                    }
                }
                #endregion
                AsmDudeToolsStatic.printSpeedWarning(time1, "CodeFoldingTagger");

                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowShutdownThresholdSec) {
                    this.disable();
                }
            }
            #endregion Payload

            this._busy = false;
            if (this._scheduled) {
                this._scheduled = false;
                this.parse_Delayed();
            }
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

        private void disable() {

            string filename = AsmDudeToolsStatic.GetFileName(this._buffer);
            string msg = string.Format("Performance of CodeFoldingTagger is horrible: disabling folding for {0}.", filename);
            AsmDudeToolsStatic.Output(string.Format("WARNING: " + msg));

            this._enabled = false;
            lock (this._updateLock) {
                this._buffer.ChangedLowPriority -= this.BufferChanged;
                this._regions.Clear();
            }
            AsmDudeToolsStatic.disableMessage(msg, filename, this._errorListProvider);
        }

        #endregion Private Methods
    }
}
