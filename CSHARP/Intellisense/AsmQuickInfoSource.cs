// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
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
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude {
    /// <summary>
    /// Factory for quick info sources
    /// </summary>
    [Export(typeof(IQuickInfoSourceProvider))]
    [ContentType("asm!")]
    [Name("asmQuickInfo")]
    class AsmQuickInfoSourceProvider : IQuickInfoSourceProvider {
        [Import]
        IBufferTagAggregatorFactoryService aggService = null;

        public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
            return new AsmQuickInfoSource(textBuffer, aggService.CreateTagAggregator<AsmTokenTag>(textBuffer));
        }
    }

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
            } catch (FileNotFoundException ex) {
                Debug.WriteLine("ERROR: AsmQuickInfoSource: could not find file \"" + filename + "\". " + ex);
            } catch (XmlException ex2) {
                Debug.WriteLine("ERROR: AsmQuickInfoSource: error while reading file \"" + filename + "\". " + ex2);
            }
        }

        /// <summary>
        /// Determine which pieces of Quickinfo content should be displayed
        /// </summary>
        public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan) {
            applicableToSpan = null;

            if (this._disposed) {
                throw new ObjectDisposedException("TestQuickInfoSource");
            }
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(_buffer.CurrentSnapshot);

            if (triggerPoint == null) {
                return;
            }
            foreach (IMappingTagSpan<AsmTokenTag> curTag in this._aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint))) {
                var tagSpan = curTag.Span.GetSpans(_buffer).First();
                string tagString = tagSpan.GetText().ToUpper();
                applicableToSpan = this._buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);


                switch (curTag.Tag.type) {
                    case AsmTokenTypes.Misc: {
                            string description = getDescriptionKeyword(tagString);
                            if (description.Length > 0) {
                                quickInfoContent.Add("Keyword " + tagString + ": " + description);
                            } else {
                                quickInfoContent.Add("Keyword " + tagString);
                            }
                            break;
                        }
                    case AsmTokenTypes.Directive: {
                            string description = getDescriptionDirective(tagString);
                            if (description.Length > 0) {
                                quickInfoContent.Add("Directive " + tagString + ": " + description);
                            } else {
                                quickInfoContent.Add("Directive " + tagString);
                            }
                            break;
                        }
                    case AsmTokenTypes.Register: {
                            string description = getDescriptionRegister(tagString);
                            if (description.Length > 0) {
                                quickInfoContent.Add("Register " + tagString + ": " + description);
                            } else {
                                quickInfoContent.Add("Register " + tagString);
                            }
                            break;
                        }
                    case AsmTokenTypes.Mnemonic: // intentional fall through
                    case AsmTokenTypes.Jump: {
                            string description = getDescriptionMnemonic(tagString);
                            if (description.Length > 0) {
                                quickInfoContent.Add("Mnemonic " + tagString + ": " + description);
                            } else {
                                quickInfoContent.Add("Mnemonic " + tagString);
                            }
                            break;
                        }
                    case AsmTokenTypes.Label: {
                            quickInfoContent.Add("Label " + tagString);
                            break;
                        }
                    case AsmTokenTypes.Constant: {
                            quickInfoContent.Add("Constant " + tagString);
                            break;
                        }
                    default:
                        break;
                }
            }
        }

        public void Dispose() {
            _disposed = true;
        }

        private string getDescriptionMnemonic(string mnemonic) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//mnemonic[@name=\"" + mnemonic + "\"]");
            if (node1 == null) {
                Debug.WriteLine("WARNING: getDescriptionMnemonic: no mnemonic element for mnemonic " + mnemonic);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: getDescriptionMnemonic: no description element for mnemonic " + mnemonic);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionMnemonic: mnemonic \"" + mnemonic + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionRegister(string register) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//register[@name=\"" + register + "\"]");
            if (node1 == null) {
                Debug.WriteLine("WARNING: getDescriptionRegister: no register element for register " + register);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: getDescriptionRegister: no description element for register " + register);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionRegister: register \"" + register + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionDirective(string directive) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//directive[@name='" + directive + "']");
            if (node1 == null) {
                Debug.WriteLine("WARNING: getDescriptionDirective: no directive element for directive " + directive);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: getDescriptionDirective: no description element for directive " + directive);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionDirective: directive \"" + directive + "\" has description \"" + description + "\"");
            return description;
        }

        private string getDescriptionKeyword(string keyword) {
            XmlNode node1 = this._xmlDoc.SelectSingleNode("//misc[@name='" + keyword + "']");
            if (node1 == null) {
                Debug.WriteLine("WARNING: getDescriptionKeyword: no misc element for keyword " + keyword);
                return "";
            }
            XmlNode node2 = node1.SelectSingleNode("./description");
            if (node2 == null) {
                Debug.WriteLine("WARNING: getDescriptionKeyword: no description element for misc " + keyword);
                return "";
            }
            string description = node2.InnerText.Trim();
            //Debug.WriteLine("INFO: getDescriptionKeyword: misc \"" + keyword + "\" has description \"" + description + "\"");
            return description;
        }
    }
}

