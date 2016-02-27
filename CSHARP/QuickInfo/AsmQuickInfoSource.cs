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
using System.Xml;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Windows;

namespace AsmDude {

    /// <summary>
    /// Provides QuickInfo information to be displayed in a text buffer
    /// </summary>
    class AsmQuickInfoSource : IQuickInfoSource {
        private ITagAggregator<AsmTokenTag> _aggregator;
        private ITextBuffer _buffer;
        private bool _disposed = false;
        private XmlDocument _xmlDoc;

        public AsmQuickInfoSource(ITextBuffer buffer, ITagAggregator<AsmTokenTag> aggregator) {
            this._aggregator = aggregator;
            this._buffer = buffer;
            this._xmlDoc = new XmlDocument();

            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filenameData = "AsmDudeData.xml";
            string filenameDll = "AsmDude.dll";
            string filename = fullPath.Substring(0, fullPath.Length - filenameDll.Length) + filenameData;
            Debug.WriteLine("INFO: AsmQuickInfoSource: going to load file \"" + filename + "\"");
            try {
                this._xmlDoc.Load(filename);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmQuickInfoSource: could not find file \"" + filename + "\".");
            } catch (XmlException) {
                MessageBox.Show("ERROR: AsmQuickInfoSource: error while reading file \"" + filename + "\".");
            }
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
                string tagString = tagSpan.GetText().ToUpper();
                applicableToSpan = this._buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);


                string description = null;

                switch (curTag.Tag.type) {
                    case AsmTokenTypes.Misc: {
                            string descr = getDescriptionKeyword(tagString);
                            description = (descr.Length > 0) ? ("Keyword " + tagString + ": " + descr) : "Keyword " + descr;
                            break;
                        }
                    case AsmTokenTypes.Directive: {
                            string descr = getDescriptionDirective(tagString);
                            description = (descr.Length > 0) ? ("Directive " + tagString + ": " + descr) : "Directive " + descr;
                            break;
                        }
                    case AsmTokenTypes.Register: {
                            string descr = getDescriptionRegister(tagString);
                            description = (descr.Length > 0) ? (tagString + ": " + descr) : "Register " + descr;
                            break;
                        }
                    case AsmTokenTypes.Mnemonic: // intentional fall through
                    case AsmTokenTypes.Jump: {
                            string descr = getDescriptionMnemonic(tagString);
                            description = (descr.Length > 0) ? ("Mnemonic " + tagString + ": " + descr) : "Mnemonic " + descr;
                            break;
                        }
                    case AsmTokenTypes.Label: {
                            description = "Label " + tagString;
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

        private string getDescriptionMnemonic(string mnemonic) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//mnemonic[@name=\"" + mnemonic + "\"]");
            if (node1 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionMnemonic: no mnemonic element for mnemonic " + mnemonic);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionMnemonic: no description element for mnemonic " + mnemonic);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionMnemonic: mnemonic \"" + mnemonic + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionRegister(string register) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//register[@name=\"" + register + "\"]");
            if (node1 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionRegister: no register element for register " + register);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionRegister: no description element for register " + register);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionRegister: register \"" + register + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionDirective(string directive) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//directive[@name='" + directive + "']");
            if (node1 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionDirective: no directive element for directive " + directive);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionDirective: no description element for directive " + directive);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionDirective: directive \"" + directive + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionKeyword(string keyword) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//misc[@name='" + keyword + "']");
            if (node1 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionKeyword: no misc element for keyword " + keyword);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: AsmQuickInfoSource:getDescriptionKeyword: no description element for misc " + keyword);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionKeyword: misc \"" + keyword + "\" has description \"" + description + "\"");
            return description;
        }

        #endregion private stuff
    }
}

