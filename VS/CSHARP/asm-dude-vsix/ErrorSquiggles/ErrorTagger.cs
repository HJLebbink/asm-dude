using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;

namespace AsmDude.ErrorSquiggles {
    sealed class ErrorTagger : ITagger<ErrorTag> {

        ITextView View { get; set; }
        ITextBuffer SourceBuffer { get; set; }
        SnapshotPoint? CurrentChar { get; set; }

        internal ErrorTagger(ITextView view, ITextBuffer sourceBuffer) {
            this.View = view;
            this.SourceBuffer = sourceBuffer;
            this.CurrentChar = null;

            this.View.Caret.PositionChanged += CaretPositionChanged;
            this.View.LayoutChanged += ViewLayoutChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (spans.Count == 0) {  //there is no content in the buffer
                yield break;
            }
            //don't do anything if the current SnapshotPoint is not initialized or at the end of the buffer
            if (!CurrentChar.HasValue || CurrentChar.Value.Position >= CurrentChar.Value.Snapshot.Length) {
                yield break;
            }
            if (false) {
                //hold on to a snapshot of the current character
                SnapshotPoint currentChar = CurrentChar.Value;

                //if the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
                if (spans[0].Snapshot != currentChar.Snapshot) {
                    currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
                }

                //get the current char and the previous char
                char currentText = currentChar.GetChar();
                SnapshotPoint lastChar = currentChar == 0 ? currentChar : currentChar - 1; //if currentChar is 0 (beginning of buffer), don't move it back
                char lastText = lastChar.GetChar();

                yield return new TagSpan<ErrorTag>(spans[0], new ErrorTag("smell"));
            } else {
                yield break;
            }
        }

        void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
            if (e.NewSnapshot != e.OldSnapshot) {//make sure that there has really been a change
                UpdateAtCaretPosition(View.Caret.Position);
            }
        }
        void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
            UpdateAtCaretPosition(e.NewPosition);
        }
        void UpdateAtCaretPosition(CaretPosition caretPosition) {
            CurrentChar = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);

            if (!CurrentChar.HasValue) {
                return;
            }
            var tempEvent = TagsChanged;
            if (tempEvent != null) {
                tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0,
                    SourceBuffer.CurrentSnapshot.Length)));
            }
        }
    }

}
