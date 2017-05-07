// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using System.Runtime.InteropServices;
using AsmDude.Tools;
using System.Windows.Media;
using AsmTools;

namespace AsmDude.HighlightWord
{
    [Export(typeof(EditorFormatDefinition))]
    [Name("AsmDude.HighlightWordFormatDefinition")]
    [UserVisible(true)]
    internal class HighlightWordFormatDefinition : MarkerFormatDefinition
    {
        public HighlightWordFormatDefinition()
        {
            //Debug.WriteLine("INFO: Entering constructor for: {0}", this.ToString());
            this.DisplayName = "AsmDude - Highlight Word";

            if (Settings.Default.KeywordHighlighting_BackgroundColor_On)
            {
                this.BackgroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.KeywordHighlighting_BackgroundColor);
            }
            if (Settings.Default.KeywordHighlighting_BorderColor_On)
            {
                this.Border = new Pen(new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.KeywordHighlighting_BorderColor)), 1);
            }
            this.ZOrder = 5; // do not know where the ZOrder is useful for
        }
    }

    /// <summary>
    /// Derive from TextMarkerTag, in case anyone wants to consume
    /// just the HighlightWordTags by themselves.
    /// </summary>
    [ComVisible(false)]
    public class HighlightWordTag : TextMarkerTag
    {
        public HighlightWordTag() : base("AsmDude.HighlightWordFormatDefinition")
        {
            // empty
        }
    }

    /// <summary>
    /// This tagger will provide tags for every word in the buffer that
    /// matches the word currently under the cursor.
    /// </summary>
    internal sealed class HighlightWordTagger : ITagger<HighlightWordTag>
    {
        private readonly ITextView _view;
        private readonly ITextBuffer _sourceBuffer;
        private readonly ITextSearchService _textSearchService;
        private object _updateLock = new object();

        // The current set of words to highlight
        private NormalizedSnapshotSpanCollection _wordSpans;

        private string CurrentWord { get; set; }
        private SnapshotSpan? _currentWordSpan;

        private string NewWord { get; set; }
        private SnapshotSpan? NewWordSpan { get; set; }

        // The current request, from the last cursor movement or view render
        private SnapshotPoint RequestedPoint { get; set; }

        public HighlightWordTagger(ITextView view, ITextBuffer sourceBuffer, ITextSearchService textSearchService)
        {
            this._view = view;
            this._sourceBuffer = sourceBuffer;
            this._textSearchService = textSearchService;

            this._wordSpans = new NormalizedSnapshotSpanCollection();

            this.CurrentWord = null;
            this._currentWordSpan = null;
            this.NewWord = null;
            this.NewWordSpan = null;

            // Subscribe to both change events in the view - any time the view is updated
            // or the caret is moved, we refresh our list of highlighted words.
            this._view.Caret.PositionChanged += this.CaretPositionChanged;
            this._view.LayoutChanged += this.ViewLayoutChanged;
        }

        #region Event Handlers

        /// <summary>
        /// Force an update if the view layout changes
        /// </summary>
        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (Settings.Default.KeywordHighlighting_BackgroundColor_On || Settings.Default.KeywordHighlighting_BorderColor_On)
            {
                // If a new snapshot wasn't generated, then skip this layout
                if (e.NewViewState.EditSnapshot != e.OldViewState.EditSnapshot)
                {
                    UpdateAtCaretPosition(this._view.Caret.Position);
                }
            }
        }

        /// <summary>
        /// Force an update if the caret position changes
        /// </summary>
        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            if (Settings.Default.KeywordHighlighting_BackgroundColor_On || Settings.Default.KeywordHighlighting_BorderColor_On)
            {
                UpdateAtCaretPosition(e.NewPosition);
            }
        }

        /// <summary>
        /// Check the caret position. If the caret is on a new word, update the CurrentWord value
        /// </summary>
        private void UpdateAtCaretPosition(CaretPosition caretPoisition)
        {
            SnapshotPoint? point = caretPoisition.Point.GetPoint(this._sourceBuffer, caretPoisition.Affinity);

            // If the new cursor position is still within the current word (and on the same snapshot),
            // we don't need to check it.
            TextExtent? newWordExtend = AsmDudeToolsStatic.Get_Keyword(point);
            if (newWordExtend.HasValue)
            {
                string newWord = newWordExtend.Value.Span.GetText();

                if ((this.CurrentWord != null) && newWord.Equals(this.CurrentWord))
                {
                    return;
                }
                else
                {
                    this.RequestedPoint = point.Value;
                    this.NewWord = newWord;
                    this.NewWordSpan = newWordExtend.Value.Span;

                    AsmDudeTools.Instance.Thread_Pool.QueueWorkItem(this.Update_Word_Adornments);
                }
            }
        }

        /// <summary>
        /// The currently highlighted word has changed. Update the adornments to reflect this change
        /// </summary>
        private void Update_Word_Adornments()
        {
            try
            {
                DateTime time1 = DateTime.Now;

                if (this.NewWord.Length > 0)
                {
                    ITextSnapshot s = this.RequestedPoint.Snapshot;
                    SnapshotSpan sp = this.NewWordSpan.Value;

                    // Find the new spans
                    FindData findData;
                    Rn reg = AsmTools.RegisterTools.ParseRn(this.NewWord);
                    if (reg != Rn.NOREG) 
                    {
                        //Debug.WriteLine(string.Format("INFO: {0}:SynchronousUpdate. Register={1}", this.ToString(), currentWordStr));
                        findData = new FindData(AsmTools.RegisterTools.GetRelatedRegister(reg), s)
                        {
                            FindOptions = FindOptions.WholeWord | FindOptions.SingleLine | FindOptions.UseRegularExpressions
                        };
                    }
                    else
                    {
                        //Debug.WriteLine(string.Format("INFO: {0}:SynchronousUpdate. Keyword={1}", this.ToString(), currentWordStr));
                        //We have to replace all occurrences of special characters with escaped versions of that char since we cannot use verbatim strings.
                        string t = this.NewWord.Replace(".", "\\.").Replace("$", "\\$").Replace("?", "\\?").Replace("/", "\\/"); //TODO escape backslashes
                        findData = new FindData(t, s)
                        {
                            FindOptions = FindOptions.WholeWord | FindOptions.SingleLine | FindOptions.UseRegularExpressions
                        };
                    }

                    List<SnapshotSpan> wordSpans = new List<SnapshotSpan>();
                    try
                    {
                        wordSpans.AddRange(this._textSearchService.FindAll(findData));
                    }
                    catch (Exception e2)
                    {
                        AsmDudeToolsStatic.Output_WARNING(string.Format("could not highlight string \"{0}\"; e={1}", findData.SearchString, e2.InnerException.Message));
                    }
                    SynchronousUpdate(this.RequestedPoint, new NormalizedSnapshotSpanCollection(wordSpans), this.NewWord, sp);
                }
                else
                {
                    // If we couldn't find a word, just clear out the existing markers
                    SynchronousUpdate(this.RequestedPoint, new NormalizedSnapshotSpanCollection(), null, null);
                }
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "HighlightWordTagger");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:UpdateWordAdornments; e={1}", ToString(), e.ToString()));
            }
        }

        /// <summary>
        /// Determine if a given "word" should be highlighted
        /// </summary>
        static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word)
        {
            return word.IsSignificant && currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c));
        }

        /// <summary>
        /// Perform a synchronous update, in case multiple background threads are running
        /// </summary>
        private void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, string newCurrentWord, SnapshotSpan? newCurrentWordSpan)
        {
            lock (this._updateLock)
            {
                if (currentRequest != this.RequestedPoint)
                {
                    return;
                }
                this._wordSpans = newSpans;
                this.CurrentWord = newCurrentWord;
                this._currentWordSpan = newCurrentWordSpan;

                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(this._sourceBuffer.CurrentSnapshot, 0, this._sourceBuffer.CurrentSnapshot.Length)));
            }
        }

        #endregion

        #region ITagger<HighlightWordTag> Members

        /// <summary>
        /// Find every instance of CurrentWord in the given span
        /// </summary>
        /// <param name="spans">A read-only span of text to be searched for instances of CurrentWord</param>
        public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (this.CurrentWord == null)
            {
                yield break;
            }
            if ((spans.Count == 0) || (this._wordSpans.Count == 0))
            {
                yield break;
            }

            // Hold on to a "snapshot" of the word spans and current word, so that we maintain the same
            // collection throughout
            SnapshotSpan currentWordLocal = this._currentWordSpan.Value;
            NormalizedSnapshotSpanCollection wordSpansLocal = this._wordSpans;

            // If the requested snapshot isn't the same as the one our words are on, translate our spans
            // to the expected snapshot
            if (spans[0].Snapshot != wordSpansLocal[0].Snapshot)
            {
                wordSpansLocal = new NormalizedSnapshotSpanCollection(
                    wordSpansLocal.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWordLocal = currentWordLocal.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            //Debug.WriteLine("INFO: GetTags: currentWord=" + currentWordLocal.GetText());

            // First, yield back the word the cursor is under (if it overlaps)
            // Note that we'll yield back the same word again in the word spans collection;
            // the duplication here is expected.
            if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWordLocal)))
            {
                yield return new TagSpan<HighlightWordTag>(currentWordLocal, new HighlightWordTag());
            }

            // Second, yield all the other words in the file
            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpansLocal))
            {
                yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag());
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion
    }
}
