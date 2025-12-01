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

namespace AsmDude.BraceMatching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;

    /// <summary>
    /// Somewhat unnecessary brace matching functionality
    /// </summary>
    internal sealed class BraceMatchingTagger : ITagger<TextMarkerTag>
    {
        private readonly ITextView view_;
        private readonly ITextBuffer sourceBuffer_;
        private readonly Dictionary<char, char> braceList_;
        private SnapshotPoint? currentChar_;

        internal BraceMatchingTagger(ITextView view, ITextBuffer buffer)
        {
            this.view_ = view ?? throw new ArgumentNullException(nameof(view));
            this.sourceBuffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));

            if (buffer.CurrentSnapshot.LineCount < AsmDudeToolsStatic.MaxFileLines)
            {
                this.currentChar_ = null;

                //here the keys are the open braces, and the values are the close braces
                this.braceList_ = new Dictionary<char, char>
                {
                    { '[', ']' },
                    { '(', ')' },
                    { '{', '}' },
                };
                this.view_.Caret.PositionChanged += this.CaretPositionChanged;
                this.view_.LayoutChanged += this.ViewLayoutChanged;
            }
            else
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:BraceMatchingTagger; file {1} contains {2} lines which is more than maxLines {3}; switching off brace matching", this.ToString(), AsmDudeToolsStatic.GetFilename(buffer), buffer.CurrentSnapshot.LineCount, AsmDudeToolsStatic.MaxFileLines));
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot)
            { //make sure that there has really been a change
                this.UpdateAtCaretPosition(this.view_.Caret.Position);
            }
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            this.UpdateAtCaretPosition(e.NewPosition);
        }

        private void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            this.currentChar_ = caretPosition.Point.GetPoint(this.sourceBuffer_, caretPosition.Affinity);

            if (!this.currentChar_.HasValue)
            {
                return;
            }
            this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(this.sourceBuffer_.CurrentSnapshot, 0, this.sourceBuffer_.CurrentSnapshot.Length)));
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            { //there is no content in the buffer
                yield break;
            }
            //don't do anything if the current SnapshotPoint is not initialized or at the end of the buffer
            if (!this.currentChar_.HasValue || this.currentChar_.Value.Position >= this.currentChar_.Value.Snapshot.Length)
            {
                yield break;
            }
            //hold on to a snapshot of the current character
            SnapshotPoint currentChar = this.currentChar_.Value;

            //if the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
            if (spans[0].Snapshot != currentChar.Snapshot)
            {
                currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
            }

            //get the current char and the previous char
            char currentText = currentChar.GetChar();
            SnapshotPoint lastChar = currentChar == 0 ? currentChar : currentChar - 1; //if currentChar is 0 (beginning of buffer), don't move it back
            char lastText = lastChar.GetChar();
            SnapshotSpan pairSpan = default(SnapshotSpan);

            if (this.braceList_.ContainsKey(currentText))
            { //the key is the open brace
                this.braceList_.TryGetValue(currentText, out char closeChar);
                if (FindMatchingCloseChar(currentChar, currentText, closeChar, this.view_.TextViewLines.Count, out pairSpan) == true)
                {
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(currentChar, 1), new TextMarkerTag("blue"));
                    yield return new TagSpan<TextMarkerTag>(pairSpan, new TextMarkerTag("blue"));
                }
            }
            else if (this.braceList_.ContainsValue(lastText))
            { //the value is the close brace, which is the *previous* character
                IEnumerable<char> open = from n in this.braceList_
                                         where n.Value.Equals(lastText)
                                         select n.Key;
                if (FindMatchingOpenChar(lastChar, open.ElementAt(0), lastText, this.view_.TextViewLines.Count, out pairSpan) == true)
                {
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(lastChar, 1), new TextMarkerTag("blue"));
                    yield return new TagSpan<TextMarkerTag>(pairSpan, new TextMarkerTag("blue"));
                }
            }
        }

        private static bool FindMatchingCloseChar(SnapshotPoint startPoint, char open, char close, int maxLines, out SnapshotSpan pairSpan)
        {
            pairSpan = new SnapshotSpan(startPoint.Snapshot, 1, 1);
            ITextSnapshotLine line = startPoint.GetContainingLine();
            string lineText = line.GetText();
            int lineNumber = line.LineNumber;
            int offset = startPoint.Position - line.Start.Position + 1;

            int stopLineNumber = startPoint.Snapshot.LineCount - 1;
            if (maxLines > 0)
            {
                stopLineNumber = Math.Min(stopLineNumber, lineNumber + maxLines);
            }
            int openCount = 0;
            while (true)
            {
                //walk the entire line
                while (offset < line.Length)
                {
                    char currentChar = lineText[offset];
                    if (currentChar == close)
                    { //found the close character
                        if (openCount > 0)
                        {
                            openCount--;
                        }
                        else
                        { //found the matching close
                            pairSpan = new SnapshotSpan(startPoint.Snapshot, line.Start + offset, 1);
                            return true;
                        }
                    }
                    else if (currentChar == open)
                    { // this is another open
                        openCount++;
                    }
                    offset++;
                }

                //move on to the next line
                if (++lineNumber > stopLineNumber)
                {
                    break;
                }
                line = line.Snapshot.GetLineFromLineNumber(lineNumber);
                lineText = line.GetText();
                offset = 0;
            }

            return false;
        }

        private static bool FindMatchingOpenChar(SnapshotPoint startPoint, char open, char close, int maxLines, out SnapshotSpan pairSpan)
        {
            pairSpan = new SnapshotSpan(startPoint, startPoint);

            ITextSnapshotLine line = startPoint.GetContainingLine();

            int lineNumber = line.LineNumber;
            int offset = startPoint - line.Start - 1; //move the offset to the character before this one

            //if the offset is negative, move to the previous line
            if (offset < 0)
            {
                line = line.Snapshot.GetLineFromLineNumber(--lineNumber);
                offset = line.Length - 1;
            }

            string lineText = line.GetText();

            int stopLineNumber = 0;
            if (maxLines > 0)
            {
                stopLineNumber = Math.Max(stopLineNumber, lineNumber - maxLines);
            }
            int closeCount = 0;

            while (true)
            {
                // Walk the entire line
                while (offset >= 0)
                {
                    char currentChar = lineText[offset];

                    if (currentChar == open)
                    {
                        if (closeCount > 0)
                        {
                            closeCount--;
                        }
                        else
                        { // We've found the open character
                            pairSpan = new SnapshotSpan(line.Start + offset, 1); //we just want the character itself
                            return true;
                        }
                    }
                    else if (currentChar == close)
                    {
                        closeCount++;
                    }
                    offset--;
                }

                // Move to the previous line
                if (--lineNumber < stopLineNumber)
                {
                    break;
                }
                line = line.Snapshot.GetLineFromLineNumber(lineNumber);
                lineText = line.GetText();
                offset = line.Length - 1;
            }
            return false;
        }
    }
}
