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


using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AsmDude {

    /*
    internal class CompletionTooltipCustomization : TextBlock {

        [Export(typeof(IUIElementProvider<Completion, ICompletionSession>))]
        [Name("SampleCompletionTooltipCustomization")]
        //Roslyn is the default Tooltip Provider. We must override it if we wish to use custom tooltips
        [Order(Before = "RoslynToolTipProvider")]
        [ContentType("text")]
        internal class CompletionTooltipCustomizationProvider : IUIElementProvider<Completion, ICompletionSession> {
            public UIElement GetUIElement(Completion itemToRender, ICompletionSession context, UIElementType elementType) {
                if (elementType == UIElementType.Tooltip) {

                    var c = new CompletionTooltipCustomization(itemToRender);
                    //c.FontFamily = new FontFamily("Fixedsys");
                    //c.FontSize = 20;
                    //c.MouseEnter += new MouseEventHandler(this.handleMouseEvent);
                    c.PreviewMouseDown += new MouseButtonEventHandler(this.handleMouseButton);
                    c.ToolTipClosing += C_ToolTipClosing;
                    context.Committed += Context_Committed;
                    return c;
                } else {
                    return null;
                }
            }

            private void Context_Committed(object sender, EventArgs e) {
                Debug.WriteLine("INFO: Context_Committed:");
            }

            private void C_ToolTipClosing(object sender, ToolTipEventArgs e) {
                Debug.WriteLine("INFO: C_ToolTipClosing:");
            }

            void handleMouseButton(object sender, MouseButtonEventArgs a) {
                Debug.WriteLine("INFO: handleMouseButton:");
            }
            void handleMouseEvent(object sender, MouseEventArgs a) {
                Debug.WriteLine("INFO: handleMouseEvent:");
            }
            void handleContextMenu(object sender, ContextMenuEventArgs a) {
                Debug.WriteLine("INFO: handleContextMenu:");
            }
        }

        /// <summary>
        /// Custom constructor enables us to modify the text values of the tooltip. In this case, we are just modifying the font style and size
        /// </summary>
        /// <param name="completion">The tooltip to be modified</param>
        internal CompletionTooltipCustomization(Completion completion) {
            Text = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", completion.DisplayText, completion.Description);
            //FontSize = 24;
            FontStyle = FontStyles.Italic;
        }
    }
    */
    class AsmCompletionSource : ICompletionSource {
        private ITextBuffer _buffer;
        private bool _disposed = false;
        private IDictionary<string, string> _keywords;
        private IDictionary<string, string> _types;
        private IDictionary<string, ImageSource> _icons;
        private IDictionary<string, string> _grammar;

        public AsmCompletionSource(ITextBuffer buffer) {
            this._buffer = buffer;

            this._keywords = new SortedDictionary<string, string>();
            this._types = new Dictionary<string, string>();
            this._icons = new Dictionary<string, ImageSource>();
            this._grammar = new Dictionary<string, string>();

            #region Grammar

            this._grammar["MOV"] = "<reg>,<reg>|<reg>,<mem>|<mem>,<reg>|<reg>,<const>|<mem>,<const>".ToUpper();
            this._grammar["LEA"] = "<reg32>,<mem>".ToUpper();
            this._grammar["PUSH"] = "<reg32>|<mem>|<const32>".ToUpper();
            this._grammar["POP"] = "<reg32>|<mem>".ToUpper();


            this._grammar["MEM8"] = "byte ptr [<reg32>]|[<reg32>+<reg32>]|<reg32>+2*<reg32>|<reg32>+4*<reg32>|<reg32>+8*<reg32>".ToUpper();
            this._grammar["MEM32"] = "dword ptr [<reg32>]|[<reg32>+<reg32>]|<reg32>+2*<reg32>|<reg32>+4*<reg32>|<reg32>+8*<reg32>".ToUpper();
            #endregion

            #region load xml
            //TODO: Ugly: better to have one place to read AsmDudeData.xml  
            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filenameDll = "AsmDude.dll";
            string installPath = fullPath.Substring(0, fullPath.Length - filenameDll.Length);

            string filenameXml = "AsmDudeData.xml";
            string filenameXmlFull = installPath + filenameXml;
            Debug.WriteLine("INFO: AsmCompletionSource: going to load file \"" + filenameXmlFull + "\"");
            try {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filenameXmlFull);

                XmlNodeList all = xmlDoc.SelectNodes("//*[@name]"); // select everything with a name attribute
                for (int i = 0; i < all.Count; i++) {
                    XmlNode node = all.Item(i);
                    if (node != null) {
                        var nameAttribute = node.Attributes["name"];
                        if (nameAttribute != null) {
                            string name = nameAttribute.Value.ToUpper();
                            var archAttribute = node.Attributes["arch"];
                            var descriptionNode = node.SelectSingleNode("./description");
                            string archStr = (archAttribute == null) ? "" : " [" + archAttribute.Value + "]";
                            string descriptionStr = (descriptionNode == null) ? "" : " - " + descriptionNode.InnerText.Trim();
                            this._keywords[name] = name + archStr + descriptionStr;
                            //this._keywords[name] = name.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                            this._types[name] = node.Name;
                            //Debug.WriteLine("INFO: AsmCompletionSource: keyword \"" + name + "\" has type "+ this._types[name]);
                        }
                    }
                }
            } catch (FileNotFoundException ex) {
                Debug.WriteLine("ERROR: AsmCompletionSource: could not find file \"" + filenameXmlFull + "\". " + ex);
            } catch (XmlException ex2) {
                Debug.WriteLine("ERROR: AsmCompletionSource: error while reading file \"" + filenameXmlFull + "\". " + ex2);
            }
            #endregion

            #region load icons

            this._icons["register"] = bitmapFromUri(new Uri(installPath + "images/icon-R-blue.png"));
            this._icons["mnemonic"] = bitmapFromUri(new Uri(installPath + "images/icon-M.png"));
            this._icons["misc"] = bitmapFromUri(new Uri(installPath + "images/icon-question.png"));

            #endregion
        }

        public static ImageSource bitmapFromUri(Uri bitmapUri) {
            var bitmap = new BitmapImage();
            try {
                bitmap.BeginInit();
                bitmap.UriSource = bitmapUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            } catch (Exception e) {
                Debug.WriteLine("WARNING: bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            if (_disposed) throw new ObjectDisposedException("AsmCompletionSource");

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
            if (triggerPoint == null) return;

            var line = triggerPoint.GetContainingLine();
            SnapshotPoint start = triggerPoint;

            // find the start of the current keyword, a whiteSpace, ",", ";", "[" and "]" also are keyword separators
            while ((start > line.Start) && !isSeparatorChar((start - 1).GetChar())) {
                start -= 1;
            }
            if (start.Position == triggerPoint.Position) return;
            //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: start" + start.Position + "; triggerPoint=" + triggerPoint.Position);

            var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
            string partialKeyword = applicableTo.GetText(snapshot);

            //if (true)
            //{ // find the previous keyword

            //}

            bool useCapitals = isAllUpper(partialKeyword);
            partialKeyword = partialKeyword.ToUpper();

            //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: partial keyword \"" + partialKeyword + "\", useCapitals="+ useCapitals);
            List<Completion> completions = new List<Completion>();
            foreach (KeyValuePair<string, string> entry in this._keywords) {

                bool selected = true;// entry.Key.Contains(partialKeyword);
                //bool selected = entry.Key.StartsWith(partialKeyword);
                //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: key=" + entry.Key + "; partialKeyword=" + partialKeyword+"; contains="+ selected);

                if (selected) {
                    //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");

                    // by default, the entry.Key is with capitals
                    string insertionText = (useCapitals) ? entry.Key : entry.Key.ToLower();
                    String description = null; //"file:H:\\Dropbox\\sc\\GitHub\\asm-dude\\html\\AAA.html";
                    ImageSource imageSource = null;
                    if (this._types[entry.Key] != null) {
                        if (this._icons.ContainsKey(this._types[entry.Key])) {
                            imageSource = this._icons[this._types[entry.Key]];
                        }
                    }

                    var c = new Completion(entry.Value, insertionText, description, imageSource, null);
                    completions.Add(c);
                }
            };

            var cc = new CompletionSet("Tokens", "Tokens", applicableTo, completions, Enumerable.Empty<Completion>());
            completionSets.Add(cc);
        }

        private static bool isSeparatorChar(char c) {
            return char.IsWhiteSpace(c) || c.Equals(',') || c.Equals('[') || c.Equals(']');
        }

        private static bool isAllUpper(string input) {
            for (int i = 0; i < input.Length; i++) {
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i])) {
                    return false;
                }
            }
            return true;
        }

        private static string getPreviousKeyword() {
            return "TODO";
        }


        public void Dispose() {
            _disposed = true;
        }
    }
}

