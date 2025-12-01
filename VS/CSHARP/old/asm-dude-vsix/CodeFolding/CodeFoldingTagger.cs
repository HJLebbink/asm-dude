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

namespace AsmDude.CodeFolding
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Windows.Controls;
    using Amib.Threading;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal class PartialRegion
    {
        public int StartLine { get; set; }

        public int StartOffset { get; set; }

        public int StartOffsetHoverText { get; set; }

        public int Level { get; set; }

        public PartialRegion PartialParent { get; set; }
    }

    internal class Region : PartialRegion
    {
        public int EndLine { get; set; }
    }

    internal sealed class CodeFoldingTagger : ITagger<IOutliningRegionTag>
    {
        #region Private Fields
        private readonly string startRegionTag = Settings.Default.CodeFolding_BeginTag;  //the characters that start the outlining region
        private readonly string endRegionTag = Settings.Default.CodeFolding_EndTag;      //the characters that end the outlining region

        private readonly ITextBuffer buffer_;
        private readonly ITagAggregator<AsmTokenTag> aggregator_;
        private readonly ErrorListProvider errorListProvider_;
        private ITextSnapshot snapshot_;
        private IList<Region> regions_;

        private readonly Delay delay_;
        private IWorkItemResult thread_Result_;

        private readonly object updateLock_ = new object();
        private bool enabled_;
        #endregion Private Fields

        /// <summary>Constructor</summary>
        public CodeFoldingTagger(
            ITextBuffer buffer,
            ITagAggregator<AsmTokenTag> aggregator,
            ErrorListProvider errorListProvider)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:constructor", this.ToString()));
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.aggregator_ = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            this.errorListProvider_ = errorListProvider ?? throw new ArgumentNullException(nameof(errorListProvider));

            this.snapshot_ = buffer.CurrentSnapshot;
            this.regions_ = new List<Region>();

            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:constructor; number of lines in file = {1}", this.ToString(), buffer.CurrentSnapshot.LineCount));

            this.enabled_ = true;

            if (buffer.CurrentSnapshot.LineCount >= AsmDudeToolsStatic.MaxFileLines)
            {
                this.enabled_ = false;
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:CodeFoldingTagger; file {1} contains {2} lines which is more than maxLines {3}; switching off code folding", this.ToString(), AsmDudeToolsStatic.GetFilename(buffer), buffer.CurrentSnapshot.LineCount, AsmDudeToolsStatic.MaxFileLines));
            }

            if (this.enabled_)
            {
                this.delay_ = new Delay(AsmDudePackage.MsSleepBeforeAsyncExecution, 10, AsmDudeTools.Instance.Thread_Pool);
                this.delay_.Done_Event += (o, i) =>
                {
                    if ((this.thread_Result_ != null) && (!this.thread_Result_.IsCanceled))
                    {
                        this.thread_Result_.Cancel();
                    }
                    this.thread_Result_ = AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Parse);
                };

                this.delay_.Reset();
                this.buffer_.ChangedLowPriority += this.Buffer_Changed;
            }
        }

        private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            this.delay_.Reset();
        }

        public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }
            if (Settings.Default.CodeFolding_On && this.enabled_)
            {
                //AsmDudeToolsStatic.Output_INFO("CodeFoldingTagger:GetTags:entering: IsDefaultCollapsed= " + Settings.Default.CodeFolding_IsDefaultCollapsed);

                lock (this.updateLock_)
                {
                    SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(this.snapshot_, SpanTrackingMode.EdgeExclusive);
                    int startLineNumber = entire.Start.GetContainingLine().LineNumber;
                    int endLineNumber = entire.End.GetContainingLine().LineNumber;

                    foreach (Region region in this.regions_)
                    {
                        if ((region.StartLine <= endLineNumber) && (region.EndLine >= startLineNumber))
                        {
                            ITextSnapshotLine startLine = this.snapshot_.GetLineFromLineNumber(region.StartLine);
                            ITextSnapshotLine endLine = this.snapshot_.GetLineFromLineNumber(region.EndLine);

                            string replacement = this.Get_Region_Description(startLine.GetText(), region.StartOffsetHoverText);
                            object hover = null;
                            if (true)
                            {
                                hover = Get_Hover_Text_String(region.StartLine, region.EndLine, this.snapshot_);
                            }
                            else
                            {
                                // the following line gives an STA error
                                /*
                                    System.InvalidOperationException: The calling thread must be STA, because many UI components require this.&#x000D;&#x000A;
                                    at System.Windows.Input.InputManager..ctor()&#x000D;&#x000A;
                                    at System.Windows.Input.InputManager.GetCurrentInputManagerImpl()&#x000D;&#x000A;
                                    at System.Windows.Input.KeyboardNavigation..ctor()&#x000D;&#x000A;
                                    at System.Windows.FrameworkElement.FrameworkServices..ctor()&#x000D;&#x000A;
                                    at System.Windows.FrameworkElement.EnsureFrameworkServices()&#x000D;&#x000A;
                                    at System.Windows.FrameworkElement..ctor()&#x000D;&#x000A;
                                    at AsmDude.CodeFolding.CodeFoldingTagger.Get_Hover_Text(Int32 beginLineNumber, Int32 endLineNumber, ITextSnapshot snapshot) in C:\Cloud\Dropbox\sc\GitHub\asm-dude\VS\CSHARP\asm-dude-vsix\CodeFolding\CodeFoldingTagger.cs:line 162&#x000D;&#x000A;
                                    at AsmDude.CodeFolding.CodeFoldingTagger.&lt;GetTags&gt;d__13.MoveNext() in C:\Cloud\Dropbox\sc\GitHub\asm-dude\VS\CSHARP\asm-dude-vsix\CodeFolding\CodeFoldingTagger.cs:line 122&#x000D;&#x000A;
                                    at Microsoft.VisualStudio.Text.Tagging.Implementation.TagAggregator`1.&lt;GetTagsForBuffer&gt;d__38.MoveNext()
                                 */
                                hover = this.Get_Hover_Text_TextBlock(region.StartLine, region.EndLine, this.snapshot_); // this
                            }
                            yield return new TagSpan<IOutliningRegionTag>(
                                new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End),
                                new OutliningRegionTag(Settings.Default.CodeFolding_IsDefaultCollapsed, true, replacement, hover));
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public bool Is_Enabled { get { return this.enabled_; } }

        #region Private Methods

        /// <summary>
        /// Get the description of the region that starts at the provided line content
        /// </summary>
        private string Get_Region_Description(string line, int startPos)
        {
            string description = string.Empty;
            //AsmDudeToolsStatic.Output_INFO("getRegionDescription: startPos=" + startPos + "; line=" + line);
            if (startPos < 0)
            {
                description = line;
            }
            else if (startPos < line.Length)
            {
                description = line.Substring(startPos).Trim();
            }
            return (description.Length > 0) ? description : "...";
        }

        private static string Get_Hover_Text_String(int beginLineNumber, int endLineNumber, ITextSnapshot snapshot)
        {
            StringBuilder sb = new StringBuilder();
            int numberOfLines = Math.Min(endLineNumber + 1 - beginLineNumber, 40); // do not show more than 40 lines
            for (int i = 0; i < numberOfLines; ++i)
            {
                sb.AppendLine(snapshot.GetLineFromLineNumber(beginLineNumber + i).GetText());
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Get the text to be displayed when hovering over a closed region
        /// </summary>
        private TextBlock Get_Hover_Text_TextBlock(int beginLineNumber, int endLineNumber, ITextSnapshot snapshot)
        {
            string hover_string = Get_Hover_Text_String(beginLineNumber, endLineNumber, snapshot);

            //TODO provide syntax highlighting for the next run
            TextBlock description = new TextBlock();
            System.Windows.Documents.Run run = new System.Windows.Documents.Run(hover_string); // TrimEnd to get rid of the last new line
            run.FontSize -= 1;
            description.Inlines.Add(run);
            return description;
        }

        /// <summary>
        /// Return start positions of the provided line content. tup has: 1) start of the folding position; 2) start of the description position.
        /// </summary>
        private (int startPosFolding, int startPosDescription) Is_Start_Keyword(string lineContent, int lineNumber)
        {
            (int startPos, int startPosDescription) tup = this.Is_Start_Directive_Keyword(lineContent);
            if (tup.startPos != -1)
            {
                return tup;
            }
            else
            {
                AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;
                if (usedAssember.HasFlag(AssemblerEnum.MASM))
                {
                    return this.Is_Start_Masm_Keyword(lineContent, lineNumber);
                }
                else if (usedAssember.HasFlag(AssemblerEnum.NASM_INTEL) || usedAssember.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    return this.Is_Start_Nasm_Keyword(lineContent, lineNumber);
                }
                else
                {
                    return (-1, -1);
                }
            }
        }

        /// <summary>
        /// Return start positions of the provided line content. tup has: 1) start of the folding position; 2) start of the description position.
        /// </summary>
        private (int startPos, int startPosDescription) Is_Start_Directive_Keyword(string lineContent)
        {
            int i1 = lineContent.IndexOf(this.startRegionTag, StringComparison.OrdinalIgnoreCase);
            if (i1 == -1)
            {
                return (-1, -1);
            }
            else
            {
                return (i1, i1 + this.startRegionTag.Length);
            }
        }

        /// <summary>
        /// Return start positions of the provided line content. tup has: 1) start of the folding position; 2) start of the description position.
        /// </summary>
        private (int startPosFolding, int startPosDescription) Is_Start_Masm_Keyword(string lineContent, int lineNumber)
        {
            try
            {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in AsmDudeToolsStatic.GetAsmTokenTags(this.aggregator_, lineNumber))
                {
                    if (asmTokenSpan.Tag.Type == AsmTokenType.Directive)
                    {
                        string tokenStr_upcase = asmTokenSpan.Span.GetSpans(this.buffer_)[0].GetText().ToUpperInvariant();
                        //AsmDudeToolsStatic.Output_INFO("CodeFoldingTagger:IsStartMasmKeyword: tokenStr=" + tokenStr);
                        switch (tokenStr_upcase)
                        {
                            case "SEGMENT":
                            case "MACRO":
                            case "STRUCT":
                            case ".IF":
                            case ".WHILE":
                            case "PROC":
                                {
                                    return (lineContent.Length, lineContent.Length);
                                }
                            case "EXTERN":
                            case "EXTRN": // no start region on a line with EXTERN keyword
                                {
                                    return (-1, -1);
                                }
                            default: break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Is_Start_Masm_Keyword; e={1}", this.ToString(), e.ToString()));
            }
            return (-1, -1);
        }

        /// <summary>
        /// Return start positions of the provided line content. tup has: 1) start of the folding position; 2) start of the description position.
        /// </summary>
        private (int startPos, int startPosDescription) Is_Start_Nasm_Keyword(string lineContent, int lineNumber)
        {
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in AsmDudeToolsStatic.GetAsmTokenTags(this.aggregator_, lineNumber))
            {
                if (asmTokenSpan.Tag.Type == AsmTokenType.Directive)
                {
                    string tokenStr_upcase = asmTokenSpan.Span.GetSpans(this.buffer_)[0].GetText().ToUpperInvariant();
                    //AsmDudeToolsStatic.Output_INFO("CodeFoldingTagger:IsStartMasmKeyword: tokenStr=" + tokenStr);
                    switch (tokenStr_upcase)
                    {
                        case "STRUC":
                        case "ISTRUC":
                        case "%MACRO":
                            {
                                return (lineContent.Length, lineContent.Length);
                            }
                        default: break;
                    }
                }
            }
            return (-1, -1);
        }

        private int Is_End_Keyword(string lineContent, int lineNumber)
        {
            int i1 = this.Is_End_Directive_Keyword(lineContent);
            if (i1 != -1)
            {
                return i1;
            }
            else
            {
                AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;
                if (usedAssember.HasFlag(AssemblerEnum.MASM))
                {
                    return this.Is_End_Masm_Keyword(lineContent, lineNumber);
                }
                else if (usedAssember.HasFlag(AssemblerEnum.NASM_INTEL) || usedAssember.HasFlag(AssemblerEnum.NASM_ATT))
                {
                    return this.Is_End_Nasm_Keyword(lineContent, lineNumber);
                }
                else
                {
                    return -1;
                }
            }
        }

        private int Is_End_Directive_Keyword(string lineContent)
        {
            return lineContent.IndexOf(this.endRegionTag, StringComparison.OrdinalIgnoreCase);
        }

        private int Is_End_Masm_Keyword(string lineContent, int lineNumber)
        {
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in AsmDudeToolsStatic.GetAsmTokenTags(this.aggregator_, lineNumber))
            {
                if (asmTokenSpan.Tag.Type == AsmTokenType.Directive)
                {
                    string tokenStr_upcase = asmTokenSpan.Span.GetSpans(this.buffer_)[0].GetText().ToUpperInvariant();
                    switch (tokenStr_upcase)
                    {
                        case "ENDS": // end token for SEGMENT
                        case "ENDP": // end token for PROC
                        case "ENDM": // end token for MACRO
                        //case "ENDS": // end token for STRUCT
                        case ".ENDIF": // end token for .IF
                        case ".ENDW": // end token for .WHILE
                            {
                                return lineContent.IndexOf(tokenStr_upcase, StringComparison.OrdinalIgnoreCase);
                            }
                        default: break;
                    }
                }
            }
            return -1;
        }

        private int Is_End_Nasm_Keyword(string lineContent, int lineNumber)
        {
            foreach (IMappingTagSpan<AsmTokenTag> asmTokenSpan in AsmDudeToolsStatic.GetAsmTokenTags(this.aggregator_, lineNumber))
            {
                if (asmTokenSpan.Tag.Type == AsmTokenType.Directive)
                {
                    string tokenStr = asmTokenSpan.Span.GetSpans(this.buffer_)[0].GetText().ToUpper(CultureInfo.InvariantCulture);
                    switch (tokenStr)
                    {
                        case "ENDSTRUC": // end token for STRUC
                        case "IEND": // end token for ISTRUC
                        case "%ENDMACRO": // end token for %MACRO
                            {
                                return lineContent.IndexOf(tokenStr, StringComparison.OrdinalIgnoreCase);
                            }
                        default: break;
                    }
                }
            }
            return -1;
        }

        private void Parse()
        {
            if (!this.enabled_)
            {
                return;
            }

            lock (this.updateLock_)
            {
                DateTime time1 = DateTime.Now;

                ITextSnapshot newSnapshot = this.buffer_.CurrentSnapshot;
                IList<Region> newRegions = new List<Region>();

                // keep the current (deepest) partial region, which will have
                // references to any parent partial regions.
                PartialRegion currentRegion = null;

                IEnumerator<ITextSnapshotLine> enumerator = newSnapshot.Lines.GetEnumerator();

                ITextSnapshotLine line = null;
                bool hasNext = enumerator.MoveNext();
                bool already_advanced = true;
                if (hasNext)
                {
                    line = enumerator.Current;
                }

                while (hasNext)
                {
                    already_advanced = false;

                    #region Parse Line
                    if (line.Length > 0)
                    {
                        string lineContent = line.GetText();
                        int lineNumber = line.LineNumber;

                        (int regionStart, int regionStartHoverText) = this.Is_Start_Keyword(lineContent, lineNumber);
                        if (regionStart != -1)
                        {
                            Add_Start_Region(lineContent, regionStart, lineNumber, regionStartHoverText, ref currentRegion, newRegions);
                        }
                        else
                        {
                            int regionEnd = this.Is_End_Keyword(lineContent, lineNumber);
                            if (regionEnd != -1)
                            {
                                Add_End_Region(lineContent, regionEnd, lineNumber, ref currentRegion, newRegions);
                            }
                            else
                            {
                                #region Search for multi-line Remark
                                if (AsmSourceTools.IsRemarkOnly(lineContent))
                                {
                                    int lineNumber2 = -1;
                                    string lineContent2 = null;

                                    while (enumerator.MoveNext())
                                    {
                                        line = enumerator.Current;
                                        string lineContent3 = line.GetText();
                                        if (AsmSourceTools.IsRemarkOnly(lineContent3) &&
                                                (this.Is_Start_Directive_Keyword(lineContent3).startPos == -1) &&
                                                (this.Is_End_Directive_Keyword(lineContent3) == -1))
                                        {
                                            lineNumber2 = line.LineNumber;
                                            lineContent2 = lineContent3;
                                            already_advanced = false;
                                        }
                                        else
                                        {
                                            already_advanced = true;
                                            break;
                                        }
                                    }
                                    if (lineNumber2 != -1)
                                    {
                                        int regionStartPos = AsmSourceTools.GetRemarkCharPosition(lineContent);
                                        Add_Start_Region(lineContent, regionStartPos, lineNumber, regionStartPos, ref currentRegion, newRegions);
                                        //this.updateChangedSpans(newSnapshot, newRegions);
                                        Add_End_Region(lineContent2, 0, lineNumber2, ref currentRegion, newRegions);
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                    #endregion Parse Line

                    #region Update Changed Spans
                    this.Update_Changed_Spans(newSnapshot, newRegions);
                    #endregion

                    #region Advance to next line
                    if (!already_advanced)
                    {
                        hasNext = enumerator.MoveNext();
                        if (hasNext)
                        {
                            line = enumerator.Current;
                        }
                    }
                    #endregion
                }
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "CodeFoldingTagger");

                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.SlowShutdownThresholdSec)
                {
#                   if DEBUG
                    AsmDudeToolsStatic.Output_WARNING("CodeFoldingTagger: Parse: disabled CodeFolding had I been in Release mode");
#                   else
                    this.Disable();
#                   endif
                }
            }
        }

        private static void Add_Start_Region(
            string lineContent,
            int regionStart,
            int lineNumber,
            int regionStartHoverText,
            ref PartialRegion currentRegion,
            IList<Region> newRegions)
        {
            //AsmDudeToolsStatic.Output_INFO("CodeFoldingTagger: addStartRegion");
            int currentLevel = (currentRegion != null) ? currentRegion.Level : 1;
            int newLevel = currentLevel + 1;

            //levels are the same and we have an existing region;
            //end the current region and start the next
            if ((currentLevel == newLevel) && (currentRegion != null))
            {
                newRegions.Add(new Region()
                {
                    Level = currentRegion.Level,
                    StartLine = currentRegion.StartLine,
                    StartOffset = currentRegion.StartOffset,
                    StartOffsetHoverText = regionStartHoverText,
                    EndLine = lineNumber,
                });

                currentRegion = new PartialRegion()
                {
                    Level = newLevel,
                    StartLine = lineNumber,
                    StartOffset = regionStart,
                    StartOffsetHoverText = regionStartHoverText,
                    PartialParent = currentRegion.PartialParent,
                };
            }
            //this is a new (sub)region
            else
            {
                currentRegion = new PartialRegion()
                {
                    Level = newLevel,
                    StartLine = lineNumber,
                    StartOffset = regionStart,
                    StartOffsetHoverText = regionStartHoverText,
                    PartialParent = currentRegion,
                };
            }
        }

        private static void Add_End_Region(
            string lineContent,
            int regionEnd,
            int lineNumber,
            ref PartialRegion currentRegion,
            IList<Region> newRegions)
        {
            //AsmDudeToolsStatic.Output_INFO("CodeFoldingTagger: addEndRegion: lineContent=" + lineContent + "; regionEnd=" + regionEnd + "; lineNumber=" + lineNumber);
            if (currentRegion != null)
            {
                newRegions.Add(new Region()
                {
                    Level = currentRegion.Level,
                    StartLine = currentRegion.StartLine,
                    StartOffset = currentRegion.StartOffset,
                    StartOffsetHoverText = currentRegion.StartOffsetHoverText,
                    EndLine = lineNumber,
                });
                currentRegion = currentRegion.PartialParent;
            }
        }

        private void Update_Changed_Spans(ITextSnapshot newSnapshot, IList<Region> newRegions)
        {
            //determine the changed span, and send a changed event with the new spans
            IList<Span> oldSpans =
                    new List<Span>(this.regions_.Select(r => As_Snapshot_Span(r, this.snapshot_)
                        .TranslateTo(newSnapshot, SpanTrackingMode.EdgeExclusive)
                        .Span));
            IList<Span> newSpans = new List<Span>(newRegions.Select(r => As_Snapshot_Span(r, newSnapshot).Span));

            NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
            NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);

            //the changed regions are regions that appear in one set or the other, but not both.
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
                this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(this.snapshot_, Span.FromBounds(changeStart, changeEnd))));
            }
        }

        private static SnapshotSpan As_Snapshot_Span(Region region, ITextSnapshot snapshot)
        {
            ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber(region.StartLine);
            ITextSnapshotLine endLine = (region.StartLine == region.EndLine) ? startLine : snapshot.GetLineFromLineNumber(region.EndLine);
            return new SnapshotSpan(startLine.Start + region.StartOffset, endLine.End);
        }

        private void Disable()
        {
            string filename = AsmDudeToolsStatic.GetFilename(this.buffer_);
            string msg = string.Format(AsmDudeToolsStatic.CultureUI, "Performance of CodeFoldingTagger is horrible: disabling folding for {0}.", filename);
            AsmDudeToolsStatic.Output_WARNING(msg);

            this.enabled_ = false;
            lock (this.updateLock_)
            {
                this.buffer_.ChangedLowPriority -= this.Buffer_Changed;
                this.regions_.Clear();
            }
            AsmDudeToolsStatic.Disable_Message(msg, filename, this.errorListProvider_);
        }

        #endregion Private Methods
    }
}