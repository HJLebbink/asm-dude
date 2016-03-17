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
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Windows;
using System.Windows.Media;
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
        private IDictionary<AsmTokenTypes, ImageSource> _icons;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        public AsmCompletionSource(ITextBuffer buffer) {
            this._buffer = buffer;
            this._icons = new Dictionary<AsmTokenTypes, ImageSource>();
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

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

            #region load icons
            Uri uri = null;
            string installPath = AsmDudeToolsStatic.getInstallPath();
            try {
                uri = new Uri(installPath + "images/icon-R-blue.png");
                this._icons[AsmTokenTypes.Register] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "images/icon-M.png");
                this._icons[AsmTokenTypes.Mnemonic] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "images/icon-question.png");
                this._icons[AsmTokenTypes.Misc] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "images/icon-L.png");
                this._icons[AsmTokenTypes.Label] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            #endregion
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:AugmentCompletionSession", this.ToString()));

            if (_disposed) throw new ObjectDisposedException("AsmCompletionSource");
            if (Properties.Settings.Default.CodeCompletion_On) {

                ITextSnapshot snapshot = this._buffer.CurrentSnapshot;
                SnapshotPoint triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
                if (triggerPoint == null) return;

                ITextSnapshotLine line = triggerPoint.GetContainingLine();

                //1] check if current position is in a remark
                if (isRemark(triggerPoint, line.Start)) return;

                //2] find the start of the current keyword
                SnapshotPoint start = triggerPoint;
                while ((start > line.Start) && !AsmDudeToolsStatic.isSeparatorChar((start - 1).GetChar())) {
                    start -= 1;
                }

                //3] test whether the keyword has a length larger than zero.
                if (start.Position == triggerPoint.Position) return;
                //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: start" + start.Position + "; triggerPoint=" + triggerPoint.Position);

                //4] get the word that is currently being typed
                var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
                string partialKeyword = applicableTo.GetText(snapshot);

                bool useCapitals = isAllUpper(partialKeyword);
                partialKeyword = partialKeyword.ToUpper();

                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:AugmentCompletionSession. partialKeyword={1}", this.ToString(), partialKeyword));
                IList<Completion> completions = new List<Completion>();

                if (isLabel(start-1, line.Start)) {
                    ImageSource imageSource = this._icons[AsmTokenTypes.Label];
                    var labels = this._asmDudeTools.getLabelsDictionary(this._buffer);
                    foreach (KeyValuePair<string, string> entry in labels) {
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                        completions.Add(new Completion(entry.Key, entry.Key, entry.Value, imageSource, ""));
                    }
                } else { // current keyword is not a label
                    foreach (string keyword in this._asmDudeTools.getKeywords()) {
                        string arch = this._asmDudeTools.getArchitecture(keyword);
                        bool selected = isArchSwitchedOn(arch);
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; keyword={1}; arch={2}; selected={3}", this.ToString(), entry.Key, this._arch[entry.Key], selected));
                        if (selected) {
                            //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");
                            // by default, the entry.Key is with capitals
                            string insertionText = (useCapitals) ? keyword : keyword.ToLower();
                            string archStr = (arch == null) ? "" : " ["+arch+"]";
                            string descriptionStr = this._asmDudeTools.getDescription(keyword);
                            descriptionStr = (descriptionStr == null) ? "" : " - " + descriptionStr;
                            String description = keyword + archStr + descriptionStr;
                            //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                            ImageSource imageSource = null;
                            AsmTokenTypes type = this._asmDudeTools.getAsmTokenType(keyword);
                            if (this._icons.ContainsKey(type)) {
                                imageSource = this._icons[type];
                            }
                            completions.Add(new Completion(description, insertionText, null, imageSource, ""));
                        }
                    }
                }
                completionSets.Add(new CompletionSet("Tokens", "Tokens", applicableTo, completions, Enumerable.Empty<Completion>()));
            }
        }

        private static bool isRemark(SnapshotPoint triggerPoint, SnapshotPoint lineStart) {
            // check if the line contains a ";" or a "#" before the current point
            for (SnapshotPoint pos = triggerPoint; pos >= lineStart; pos -= 1) {
                char c = pos.GetChar();
                if (c.Equals(';') || c.Equals('#')) {
                    return true;
                }
            }
            return false;
        }

        private bool isLabel(SnapshotPoint triggerPoint, SnapshotPoint lineStart) {
            return this._asmDudeTools.isJumpKeyword(getPreviousKeyword(triggerPoint, lineStart));
        }

        private static string getPreviousKeyword(SnapshotPoint triggerPoint, SnapshotPoint lineStart) {
            // find the end of previous keyword
            SnapshotPoint end = lineStart;
            SnapshotPoint pos = triggerPoint;
            for (; pos >= lineStart; pos -= 1) {
                if (!AsmDudeToolsStatic.isSeparatorChar(pos.GetChar())) {
                    end = pos+1;
                    break;
                }
            }
            SnapshotPoint begin = lineStart;
            for (; pos >= lineStart; pos -= 1) {
                if (AsmDudeToolsStatic.isSeparatorChar(pos.GetChar())) {
                    begin = pos+1;
                    break;
                }
            }
            var applicableTo = triggerPoint.Snapshot.CreateTrackingSpan(new SnapshotSpan(begin, end), SpanTrackingMode.EdgeInclusive);
            string previousKeyword = applicableTo.GetText(triggerPoint.Snapshot);
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: getPreviousKeyword; previousKeyword={0}", previousKeyword));
            return previousKeyword;
        }

        private static bool isArchSwitchedOn(string arch) {
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
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:isArchSwitchedOn; unsupported arch {0}", arch));
                    return true;
            }
        }

        private static bool isAllUpper(string input) {
            for (int i = 0; i < input.Length; i++) {
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i])) {
                    return false;
                }
            }
            return true;
        }

        public void Dispose() {
            _disposed = true;
        }
    }
}

