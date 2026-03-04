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

namespace AsmDude2.BraceMatching
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;

    /// <summary>
    /// Brace matching functionality for assembly language.
    /// Highlights matching brackets: [], (), {}
    /// </summary>
    internal sealed class BraceMatchingTagger : ITagger<TextMarkerTag>
    {
        private readonly ITextView view;
        private readonly ITextBuffer sourceBuffer;
        private readonly Dictionary<char, char> braceList;
        private SnapshotPoint? currentChar;

        private const int MaxFileLines = 10000;

        internal BraceMatchingTagger(ITextView view, ITextBuffer buffer)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.sourceBuffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            if (buffer.CurrentSnapshot.LineCount < MaxFileLines)
            {
                this.currentChar = null;

                // Open braces as keys, close braces as values
                this.braceList = new Dictionary<char, char>
                {
                    { '[', ']' },
                    { '(', ')' },
                    { '{', '}' },
                };
                this.view.Caret.PositionChanged += this.CaretPositionChanged;
                this.view.LayoutChanged += this.ViewLayoutChanged;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot)
            {
                // Snapshot changed, update
                this.UpdateAtCaretPosition(this.view.Caret.Position);
            }
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            this.UpdateAtCaretPosition(e.NewPosition);
        }

        private void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            this.currentChar = caretPosition.Point.GetPoint(this.sourceBuffer, caretPosition.Affinity);

            if (!this.currentChar.HasValue)
            {
                return;
            }
            this.TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                new SnapshotSpan(this.sourceBuffer.CurrentSnapshot, 0, this.sourceBuffer.CurrentSnapshot.Length)));
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            // Don't do anything if currentChar is not initialized or at end of buffer
            if (!this.currentChar.HasValue || this.currentChar.Value.Position >= this.currentChar.Value.Snapshot.Length)
            {
                yield break;
            }

            SnapshotPoint currentChar = this.currentChar.Value;

            // Translate spans to expected snapshot if needed
            if (spans[0].Snapshot != currentChar.Snapshot)
            {
                currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
            }

            // Get current char and previous char
            char currentText = currentChar.GetChar();
            SnapshotPoint lastChar = currentChar == 0 ? currentChar : currentChar - 1;
            char lastText = lastChar.GetChar();
            SnapshotSpan pairSpan = default;

            if (this.braceList.ContainsKey(currentText))
            {
                // Current char is an open brace
                this.braceList.TryGetValue(currentText, out char closeChar);
                if (FindMatchingCloseChar(currentChar, currentText, closeChar, this.view.TextViewLines.Count, out pairSpan))
                {
                    yield return new TagSpan<TextMarkerTag>(new SnapshotSpan(currentChar, 1), new TextMarkerTag("blue"));
                    yield return new TagSpan<TextMarkerTag>(pairSpan, new TextMarkerTag("blue"));
                }
            }
            else if (this.braceList.ContainsValue(lastText))
            {
                // Previous char is a close brace
                var open = from n in this.braceList
                           where n.Value.Equals(lastText)
                           select n.Key;
                if (FindMatchingOpenChar(lastChar, open.ElementAt(0), lastText, this.view.TextViewLines.Count, out pairSpan))
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
                // Walk the entire line
                while (offset < line.Length)
                {
                    char currentChar = lineText[offset];
                    if (currentChar == close)
                    {
                        if (openCount > 0)
                        {
                            openCount--;
                        }
                        else
                        {
                            // Found the matching close
                            pairSpan = new SnapshotSpan(startPoint.Snapshot, line.Start + offset, 1);
                            return true;
                        }
                    }
                    else if (currentChar == open)
                    {
                        openCount++;
                    }
                    offset++;
                }

                // Move to next line
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
            int offset = startPoint - line.Start - 1;

            // If offset is negative, move to previous line
            if (offset < 0)
            {
                if (lineNumber == 0)
                {
                    // Already at the first line, no matching open brace can exist before this
                    return false;
                }
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
                // Walk the entire line backwards
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
                        {
                            // Found the matching open
                            pairSpan = new SnapshotSpan(line.Start + offset, 1);
                            return true;
                        }
                    }
                    else if (currentChar == close)
                    {
                        closeCount++;
                    }
                    offset--;
                }

                // Move to previous line
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
