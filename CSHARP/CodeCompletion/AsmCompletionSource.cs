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
using System.Diagnostics;
using System.ComponentModel.Composition;
using System.Xml;
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Globalization;

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
        //private IDictionary<string, string> _grammar; // experimental
        private IDictionary<string, string> _arch; // todo make an arch enumeration

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        public AsmCompletionSource(ITextBuffer buffer) {
            this._buffer = buffer;
            this._keywords = new SortedDictionary<string, string>();
            this._types = new Dictionary<string, string>();
            this._icons = new Dictionary<string, ImageSource>();
            this._arch = new Dictionary<string, string>();

            #region Grammar
            /*
            // experimental
            this._grammar = new Dictionary<string, string>();
            this._grammar["MOV"] = "<reg>,<reg>|<reg>,<mem>|<mem>,<reg>|<reg>,<const>|<mem>,<const>".ToUpper();
            this._grammar["LEA"] = "<reg32>,<mem>".ToUpper();
            this._grammar["PUSH"] = "<reg32>|<mem>|<const32>".ToUpper();
            this._grammar["POP"] = "<reg32>|<mem>".ToUpper();

            this._grammar["MEM8"] = "byte ptr [<reg32>]|[<reg32>+<reg32>]|<reg32>+2*<reg32>|<reg32>+4*<reg32>|<reg32>+8*<reg32>".ToUpper();
            this._grammar["MEM32"] = "dword ptr [<reg32>]|[<reg32>+<reg32>]|<reg32>+2*<reg32>|<reg32>+4*<reg32>|<reg32>+8*<reg32>".ToUpper();
            */
            #endregion

            #region load xml

            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
            if (this._asmDudeTools == null) {
                MessageBox.Show("ERROR: AsmCompletionSource:_asmDudePackage is null.");
            } else {
                XmlDocument xmlDoc = this._asmDudeTools.getXmlData();
                XmlNodeList all = xmlDoc.SelectNodes("//*[@name]"); // select everything with a name attribute
                for (int i = 0; i < all.Count; i++) {
                    XmlNode node = all.Item(i);
                    if (node != null) {
                        var nameAttribute = node.Attributes["name"];
                        if (nameAttribute != null) {
                            string name = nameAttribute.Value.ToUpper();
                            string archStr;
                            var archAttribute = node.Attributes["arch"];
                            if (archAttribute == null) {
                                archStr = "";
                                this._arch[name] = null;
                            } else {
                                archStr = " [" + archAttribute.Value + "]";
                                this._arch[name] = archAttribute.Value.ToUpper();
                            }

                            var descriptionNode = node.SelectSingleNode("./description");
                            string descriptionStr = (descriptionNode == null) ? "" : " - " + descriptionNode.InnerText.Trim();
                            this._keywords[name] = name + archStr + descriptionStr;
                            //this._keywords[name] = name.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                            this._types[name] = node.Name;
                            //Debug.WriteLine("INFO: AsmCompletionSource: keyword \"" + name + "\" has type "+ this._types[name]);
                        }
                    }
                }
            }
            #endregion

            #region load icons
            Uri uri = null;
            string installPath = AsmDudeToolsStatic.getInstallPath();
            try {
                uri = new Uri(installPath + "images/icon-R-blue.png");
                this._icons["register"] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "images/icon-M.png");
                this._icons["mnemonic"] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "images/icon-question.png");
                this._icons["misc"] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            #endregion
        }


        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            if (_disposed) throw new ObjectDisposedException("AsmCompletionSource");
            if (Properties.Settings.Default.CodeCompletion_On) {

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

                bool useCapitals = isAllUpper(partialKeyword);
                partialKeyword = partialKeyword.ToUpper();

                //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: partial keyword \"" + partialKeyword + "\", useCapitals="+ useCapitals);
                List<Completion> completions = new List<Completion>();
                foreach (KeyValuePair<string, string> entry in this._keywords) {

                    bool selected = isArchSwitchedOn(this._arch[entry.Key]);
                    //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; keyword={1}; arch={2}; selected={3}", this.ToString(), entry.Key, this._arch[entry.Key], selected));

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
        }

        private bool isArchSwitchedOn(string arch) {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:isArchSwitchedOn; arch={1}", this.ToString(), arch));

            switch (arch) {
                case "X86": return Properties.Settings.Default.CodeCompletion_x86;
                case "I686": return Properties.Settings.Default.CodeCompletion_x86;
                case "MMX": return Properties.Settings.Default.CodeCompletion_mmx;
                case "SSE": return Properties.Settings.Default.CodeCompletion_sse;
                case "SSE2": return Properties.Settings.Default.CodeCompletion_sse2;
                case "SSE3": return Properties.Settings.Default.CodeCompletion_sse3;
                case "SSSE3": return Properties.Settings.Default.CodeCompletion_ssse3;
                case "SSE4.1": return Properties.Settings.Default.CodeCompletion_sse41;
                case "SSE4.2": return Properties.Settings.Default.CodeCompletion_sse42;
                case "AVX": return Properties.Settings.Default.CodeCompletion_avx;
                case "AVX2": return Properties.Settings.Default.CodeCompletion_avx2;
                case "KNC": return Properties.Settings.Default.CodeCompletion_knc;
                case null:
                case "": return true;
                default:
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:isArchSwitchedOn; unsupported arch {1}", this.ToString(), arch));
                    return true;
            }
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

