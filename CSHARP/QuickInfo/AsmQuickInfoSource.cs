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
using System.Linq;
using System.Collections.Generic;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;

namespace AsmDude.QuickInfo {

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    class AsmQuickInfoSource : IQuickInfoSource {
        private ITagAggregator<AsmTokenTag> _aggregator;
        private ITextBuffer _buffer;
        private bool _disposed = false;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        public AsmQuickInfoSource(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            this._aggregator = aggregator;
            this._buffer = buffer;
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
        }

        /// <summary>
        /// Determine which pieces of Quickinfo content should be displayed
        /// </summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            applicableToSpan = null;

            if (this._disposed) {
                throw new ObjectDisposedException("AsmQuickInfoSource");
            }
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null) {
                return;
            }

            foreach (IMappingTagSpan<AsmTokenTag> curTag in this._aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {
                var tagSpan = curTag.Span.GetSpans(_buffer).First();
                string tagString = tagSpan.GetText();
                string tagStringUpper = tagString.ToUpper();
                applicableToSpan = this._buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);

                string description = null;

                switch (curTag.Tag.type) {
                    case AsmTokenTypes.Misc: {
                            string descr = this._asmDudeTools.getDescription(tagStringUpper);
                            description = (descr.Length > 0) ? ("Keyword " + tagStringUpper + ": " + descr) : "Keyword " + tagStringUpper;
                            break;
                        }
                    case AsmTokenTypes.Directive: {
                            string descr = this._asmDudeTools.getDescription(tagStringUpper);
                            description = (descr.Length > 0) ? ("Directive " + tagStringUpper + ": " + descr) : "Directive " + tagStringUpper;
                            break;
                        }
                    case AsmTokenTypes.Register: {
                            string descr = this._asmDudeTools.getDescription(tagStringUpper);
                            description = (descr.Length > 0) ? (tagStringUpper + ": " + descr) : "Register " + tagStringUpper;
                            break;
                        }
                    case AsmTokenTypes.Mnemonic: // intentional fall through
                    case AsmTokenTypes.Jump: {
                            string descr = this._asmDudeTools.getDescription(tagStringUpper);
                            description = (descr.Length > 0) ? ("Mnemonic " + tagStringUpper + ": " + descr) : "Mnemonic " + tagStringUpper;
                            break;
                        }
                    case AsmTokenTypes.Label: {
                            string descr = this._asmDudeTools.getLabelDescription(tagString, this._buffer);
                            description = (descr.Length > 0) ? descr : "Label " + tagString;
                            break;
                        }
                    case AsmTokenTypes.Constant: {
                            description = "Constant " + tagString;
                            break;
                        }
                    default:
                        break;
                }
                if (description != null) {
                    const int maxLineLength = 100;
                    quickInfoContent.Add(multiLine(description, maxLineLength));
                }
            }
        }

        public void Dispose() {
            _disposed = true;
        }

        #region private stuff

        private static string multiLine(string strIn, int maxLineLength) {
            string result = strIn;
            int startPos = 0;
            int endPos = startPos + maxLineLength;

            while (endPos < result.Length) {
                int newLinePos = getNewLinePos(result, startPos + maxLineLength/2, endPos);
                result = result.Insert(newLinePos, System.Environment.NewLine);
                startPos = newLinePos + 1;
                endPos = startPos + maxLineLength;
            }
            return result;
        }

        private static int getNewLinePos(string str, int startPos, int endPos) {
            for (int pos = endPos; pos > startPos; pos--) {
                if (isSeparatorChar(str[pos])) {
                    return pos + 1;
                }
            }
            return endPos;
        }

        private static bool isSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']');
        }

        #endregion private stuff
    }
}

