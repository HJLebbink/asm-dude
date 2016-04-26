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
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using System.Globalization;
using System.Diagnostics;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using System.Runtime.InteropServices;

namespace AsmDude.HighlightWord {

    [Export(typeof(EditorFormatDefinition))]
    [Name("AsmDude.HighlightWordFormatDefinition")]
    [UserVisible(true)]
    internal class HighlightWordFormatDefinition : MarkerFormatDefinition {
        public HighlightWordFormatDefinition() {
            this.BackgroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.KeywordHighlightColor);
            this.DisplayName = "Highlight Word";
            this.ZOrder = 5;
        }
    }

    /// <summary>
    /// Derive from TextMarkerTag, in case anyone wants to consume
    /// just the HighlightWordTags by themselves.
    /// </summary>
 //   [ComVisible(true)]
    public class HighlightWordTag : TextMarkerTag {
        public HighlightWordTag() : base("AsmDude.HighlightWordFormatDefinition") {
            // empty
        }
    }

    /// <summary>
    /// This tagger will provide tags for every word in the buffer that
    /// matches the word currently under the cursor.
    /// </summary>
    internal sealed class HighlightWordTagger : ITagger<HighlightWordTag> {
        private ITextView _view { get; set; }
        private ITextBuffer _sourceBuffer { get; set; }
        private ITextSearchService _textSearchService { get; set; }
        private object _updateLock = new object();

        // The current set of words to highlight
        private NormalizedSnapshotSpanCollection _wordSpans { get; set; }

        private string _currentWord { get; set; }
        private SnapshotSpan? _currentWordSpan { get; set; }

        private string _newWord { get; set; }
        private SnapshotSpan? _newWordSpan { get; set; }

        // The current request, from the last cursor movement or view render
        private SnapshotPoint _requestedPoint { get; set; }

        public HighlightWordTagger(ITextView view, ITextBuffer sourceBuffer, ITextSearchService textSearchService) {
            _view = view;
            _sourceBuffer = sourceBuffer;
            _textSearchService = textSearchService;

            _wordSpans = new NormalizedSnapshotSpanCollection();

            _currentWord = null;
            _currentWordSpan = null;
            _newWord = null;
            _newWordSpan = null;

            // Subscribe to both change events in the view - any time the view is updated
            // or the caret is moved, we refresh our list of highlighted words.
            _view.Caret.PositionChanged += CaretPositionChanged;
            _view.LayoutChanged += ViewLayoutChanged;
        }

        #region Event Handlers

        /// <summary>
        /// Force an update if the view layout changes
        /// </summary>
        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
            if (Settings.Default.KeywordHighlight_On) {
                // If a new snapshot wasn't generated, then skip this layout
                if (e.NewViewState.EditSnapshot != e.OldViewState.EditSnapshot) {
                    this.UpdateAtCaretPosition(_view.Caret.Position);
                }
            }
        }

        /// <summary>
        /// Force an update if the caret position changes
        /// </summary>
        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
            if (Settings.Default.KeywordHighlight_On) {
                this.UpdateAtCaretPosition(e.NewPosition);
            }
        }

        /// <summary>
        /// Check the caret position. If the caret is on a new word, update the CurrentWord value
        /// </summary>
        private void UpdateAtCaretPosition(CaretPosition caretPoisition) {

            SnapshotPoint? point = caretPoisition.Point.GetPoint(this._sourceBuffer, caretPoisition.Affinity);

            // If the new cursor position is still within the current word (and on the same snapshot),
            // we don't need to check it.
            TextExtent? newWordExtend = AsmDudeToolsStatic.getKeyword(point);
            if (newWordExtend.HasValue) {
                string newWord = newWordExtend.Value.Span.GetText();

                if ((this._currentWord != null) && newWord.Equals(this._currentWord)) {
                    return;
                } else {
                    this._requestedPoint = point.Value;
                    this._newWord = newWord;
                    this._newWordSpan = newWordExtend.Value.Span;
                    ThreadPool.QueueUserWorkItem(UpdateWordAdornments);
                }
            }
        }

        /// <summary>
        /// The currently highlighted word has changed. Update the adornments to reflect this change
        /// </summary>
        private void UpdateWordAdornments(object threadContext) {
            try {
                DateTime time1 = DateTime.Now;

                if (this._newWord.Length > 0) {

                    ITextSnapshot s = this._requestedPoint.Snapshot;
                    SnapshotSpan sp = this._newWordSpan.Value;

                    // Find the new spans
                    FindData findData;
                    if (AsmTools.AsmSourceTools.isRegister(this._newWord)) {
                        //Debug.WriteLine(string.Format("INFO: {0}:SynchronousUpdate. Register={1}", this.ToString(), currentWordStr));
                        findData = new FindData(AsmTools.AsmSourceTools.getRelatedRegister(this._newWord), s);
                        findData.FindOptions = FindOptions.WholeWord | FindOptions.SingleLine | FindOptions.UseRegularExpressions;
                    } else {
                        //Debug.WriteLine(string.Format("INFO: {0}:SynchronousUpdate. Keyword={1}", this.ToString(), currentWordStr));
                        //We have to replace all occurrences of special characters with escaped versions of that char since we cannot use verbatim strings.
                        string t = this._newWord.Replace(".", "\\.").Replace("$", "\\$").Replace("?", "\\?").Replace("/", "\\/"); //TODO escape backslashes
                        findData = new FindData(t, s);
                        findData.FindOptions = FindOptions.SingleLine | FindOptions.UseRegularExpressions;
                    }

                    List<SnapshotSpan> wordSpans = new List<SnapshotSpan>();
                    try {
                        wordSpans.AddRange(this._textSearchService.FindAll(findData));
                    } catch (Exception e2) {
                        AsmDudeToolsStatic.Output(string.Format("WARNING: could not highlight string \"{0}\"; e={1}", findData.SearchString, e2.InnerException.Message));
                    }
                    this.SynchronousUpdate(this._requestedPoint, new NormalizedSnapshotSpanCollection(wordSpans), this._newWord, sp);
                } else {
                    // If we couldn't find a word, just clear out the existing markers
                    this.SynchronousUpdate(this._requestedPoint, new NormalizedSnapshotSpanCollection(), null, null);
                }

                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to highlight string \"{1}\".", elapsedSec, this._newWord));
                }
                //AsmDudeToolsStatic.Output(string.Format("INFO: took {0:F3} seconds to highlight string \"{1}\".", elapsedSec, this._newWord));
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:UpdateWordAdornments; e={1}", this.ToString(), e.ToString()));
            }
        }

        /// <summary>
        /// Determine if a given "word" should be highlighted
        /// </summary>
        static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word) {
            return word.IsSignificant && currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c));
        }

        /// <summary>
        /// Perform a synchronous update, in case multiple background threads are running
        /// </summary>
        private void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans, string newCurrentWord, SnapshotSpan? newCurrentWordSpan) {
            lock (_updateLock) {
                if (currentRequest != _requestedPoint) {
                    return;
                }
                _wordSpans = newSpans;
                _currentWord = newCurrentWord;
                _currentWordSpan = newCurrentWordSpan;

                var tempEvent = TagsChanged;
                if (tempEvent != null) {
                    tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(_sourceBuffer.CurrentSnapshot, 0, _sourceBuffer.CurrentSnapshot.Length)));
                }
            }
        }

        #endregion

        #region ITagger<HighlightWordTag> Members

        /// <summary>
        /// Find every instance of CurrentWord in the given span
        /// </summary>
        /// <param name="spans">A read-only span of text to be searched for instances of CurrentWord</param>
        public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (_currentWord == null) {
                yield break;
            }
            if ((spans.Count == 0) || (_wordSpans.Count == 0)) {
                yield break;
            }

            // Hold on to a "snapshot" of the word spans and current word, so that we maintain the same
            // collection throughout
            SnapshotSpan currentWordLocal = _currentWordSpan.Value;
            NormalizedSnapshotSpanCollection wordSpansLocal = _wordSpans;

            // If the requested snapshot isn't the same as the one our words are on, translate our spans
            // to the expected snapshot
            if (spans[0].Snapshot != wordSpansLocal[0].Snapshot) {
                wordSpansLocal = new NormalizedSnapshotSpanCollection(
                    wordSpansLocal.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

                currentWordLocal = currentWordLocal.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
            }

            //Debug.WriteLine("INFO: GetTags: currentWord=" + currentWordLocal.GetText());

            // First, yield back the word the cursor is under (if it overlaps)
            // Note that we'll yield back the same word again in the word spans collection;
            // the duplication here is expected.
            if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWordLocal))) {
                yield return new TagSpan<HighlightWordTag>(currentWordLocal, new HighlightWordTag());
            }

            // Second, yield all the other words in the file
            foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpansLocal)) {
                yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag());
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion
    }
}
