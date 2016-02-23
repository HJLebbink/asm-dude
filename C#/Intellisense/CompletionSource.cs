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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.ComponentModel.Composition;
using System.Xml;
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude {

    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("asm!")]
    [Name("asmCompletion")]
    class AsmCompletionSourceProvider : ICompletionSourceProvider {
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
            return new AsmCompletionSource(textBuffer);
        }
    }

    class AsmCompletionSource : ICompletionSource {
        private ITextBuffer _buffer;
        private bool _disposed = false;
        Dictionary<String, String> _keywords;

        public AsmCompletionSource(ITextBuffer buffer) {
            _buffer = buffer;
            _keywords = new Dictionary<string, String>();

            //TODO: Ugly: better to have one place to read AsmDudeData.xml  
            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filenameData = "AsmDudeData.xml";
            string filenameDll = "AsmDude.dll";
            string filename = fullPath.Substring(0, fullPath.Length - filenameDll.Length) + filenameData;
            Debug.WriteLine("INFO: AsmCompletionSource: going to load file \"" + filename + "\"");
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filename);

                XmlNodeList all = xmlDoc.SelectNodes("//*[@name]"); // select everything with a name attribute
                for (int i = 0; i < all.Count; i++)
                {
                    XmlNode node = all.Item(i);
                    if (node != null)
                    {
                        var nameAttribute = node.Attributes["name"];
                        if (nameAttribute != null)
                        {
                            string name = nameAttribute.Value.ToUpper();
                            var node2 = node.SelectSingleNode("./description");
                            if (node2 == null)
                            {
                                this._keywords[name] = name;
                            }
                            else {
                                this._keywords[name] = name + " - " + node2.InnerText.Trim();
                            }
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine("ERROR: AsmCompletionSource: could not find file \"" + filename + "\". " + ex);
            }
            catch (XmlException ex2)
            {
                Debug.WriteLine("ERROR: AsmCompletionSource: error while reading file \"" + filename + "\". " + ex2);
            }
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            if (_disposed) throw new ObjectDisposedException("AsmCompletionSource");

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
            if (triggerPoint == null) return;

            var line = triggerPoint.GetContainingLine();
            SnapshotPoint start = triggerPoint;

            while (start > line.Start && !char.IsWhiteSpace((start - 1).GetChar())) {
                start -= 1;
            }

            var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
            string partialKeyword = applicableTo.GetText(snapshot).ToUpper();
            if (partialKeyword.Length == 0) return;

            //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: partial keyword \"" + partialKeyword + "\"");
            List<Completion> completions = new List<Completion>();

            foreach (KeyValuePair<string, string> entry in this._keywords)
            {
                if (entry.Key.StartsWith(partialKeyword))
                {
                    //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");
                    completions.Add(new Completion(entry.Value, entry.Key, null, null, null));
                }
            };
            completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
        }

        public void Dispose() {
            _disposed = true;
        }
    }
}

