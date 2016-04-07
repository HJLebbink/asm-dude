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
using System.Windows.Media;
using System.Globalization;

using AsmTools;

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
        private IDictionary<TokenType, ImageSource> _icons;

        [Import]
        private AsmDudeTools _asmDudeTools = null;

        [Import]
        private AsmDudePackage _package = null;

        public AsmCompletionSource(ITextBuffer buffer) {
            this._buffer = buffer;
            this._icons = new Dictionary<TokenType, ImageSource>();
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

            this.loadIcons();

            #region Grammar experiment
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
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:AugmentCompletionSession", this.ToString()));

            if (!Settings.Default.CodeCompletion_On) {
                return;
            }
            if (_disposed) {
                throw new ObjectDisposedException("AsmCompletionSource");
            }

            try {
                DateTime time1 = DateTime.Now;
                ITextSnapshot snapshot = this._buffer.CurrentSnapshot;
                SnapshotPoint triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
                if (triggerPoint == null) {
                    return;
                }
                ITextSnapshotLine line = triggerPoint.GetContainingLine();

                //1] check if current position is in a remark; if we are in a remark, no code completion
                if (AsmCompletionSource.isRemark(triggerPoint, line.Start)) {
                    return;
                }
                //2] find the start of the current keyword
                SnapshotPoint start = triggerPoint;
                while ((start > line.Start) && !AsmTools.Tools.isSeparatorChar((start - 1).GetChar())) {
                    start -= 1;
                }
                //3] get the word that is currently being typed
                var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
                string partialKeyword = applicableTo.GetText(snapshot);
                bool useCapitals = AsmDudeToolsStatic.isAllUpper(partialKeyword);

                //4] get the previous keyword to narrow down the possible suggestions
                string previousKeyword = AsmCompletionSource.getPreviousKeyword(line.Start, start);

                IList<Completion> completions = null;

                if ((previousKeyword.Length == 0) || this.isLabel(previousKeyword)) {
                    // no previous keyword exists. Do not suggest a register
                    HashSet<TokenType> selected = new HashSet<TokenType> { TokenType.Directive, TokenType.Jump, TokenType.Misc, TokenType.Mnemonic /*, TokenType.Register*/ };
                    completions = this.selectedCompletions(useCapitals, selected);

                } else if (this.isJump(previousKeyword)) {
                    // previous keyword is jump (or call) mnemonic. Suggest "SHORT" or a label
                    completions = this.labelCompletions();
                    completions.Add(new Completion("SHORT", (useCapitals) ? "SHORT" : "short", null, this._icons[TokenType.Misc], ""));

                } else if (previousKeyword.Equals("SHORT")) {
                    // previous keyword is SHORT. Suggest a label
                    completions = this.labelCompletions();

                } else if (this.isRegister(previousKeyword)) {
                    // if the previous keyword is a register, suggest registers (of equal size), no opcodes and no directives
                    HashSet<TokenType> selected = new HashSet<TokenType> { /*TokenType.Directive, TokenType.Jump,*/ TokenType.Misc, /*TokenType.Mnemonic,*/ TokenType.Register };
                    completions = this.selectedCompletions(useCapitals, selected);

                } else if (this.isMnemonic(previousKeyword)) {
                    // if previous keyword is a mnemonic (no jump). Do not suggest a Mnemonic or directive
                    HashSet<TokenType> selected = new HashSet<TokenType> { /*TokenType.Directive, TokenType.Jump,*/ TokenType.Misc, /*TokenType.Mnemonic,*/ TokenType.Register };
                    completions = this.selectedCompletions(useCapitals, selected);

                } else {
                    HashSet<TokenType> selected = new HashSet<TokenType> { TokenType.Directive, TokenType.Jump, TokenType.Misc, TokenType.Mnemonic, TokenType.Register };
                    completions = this.selectedCompletions(useCapitals, selected);
                }
                completionSets.Add(new CompletionSet("Tokens", "Tokens", applicableTo, completions, Enumerable.Empty<Completion>()));

                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to prepare code completion for previous keyword \"{1}\" and current keyword \"{2}\".", elapsedSec, previousKeyword, partialKeyword, elapsedSec));
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:AugmentCompletionSession; e={1}", this.ToString(), e.ToString()));
            }
        }

        public void Dispose() {
            if (!this._disposed) {
                GC.SuppressFinalize(this);
                _disposed = true;
            }
        }

        #region private stuff

        private IList<Completion> labelCompletions() {
            IList<Completion> completions = new List<Completion>();
            ImageSource imageSource = this._icons[TokenType.Label];
            SortedDictionary<string, string> labels = new SortedDictionary<string, string>(AsmDudeToolsStatic.getLabels(this._buffer));

            foreach (KeyValuePair<string, string> entry in labels) { 
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                completions.Add(new Completion(entry.Key, entry.Key, entry.Value, imageSource, ""));
            }
            return completions;
        }

        private IList<Completion> selectedCompletions(bool useCapitals, HashSet<TokenType> selectedTypes) {
            IList<Completion> completions = new List<Completion>();
            foreach (string keyword in this._asmDudeTools.getKeywords()) {
                TokenType type = this._asmDudeTools.getTokenType(keyword);
                if (selectedTypes.Contains(type)) {
                    string arch = this._asmDudeTools.getArchitecture(keyword);
                    bool selected = this.isArchSwitchedOn(arch);
                    //AsmDudeToolsStatic.Output(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; keyword={1}; arch={2}; selected={3}", this.ToString(), keyword, arch, selected));

                    if (selected) {
                        //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");
                        
                        // by default, the entry.Key is with capitals
                        string insertionText = (useCapitals) ? keyword : keyword.ToLower();
                        string archStr = (arch == null) ? "" : " [" + arch + "]";
                        string descriptionStr = this._asmDudeTools.getDescription(keyword);
                        descriptionStr = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                        //String description = keyword + archStr + descriptionStr;
                        String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;

                        ImageSource imageSource = null;
                        if (this._icons.ContainsKey(type)) {
                            imageSource = this._icons[type];
                        }
                        completions.Add(new Completion(description, insertionText, null, imageSource, ""));
                    }
                }
            }
            return completions;
        }

        /// <summary>
        /// Determine whether the provided triggerPoint is in a remark.
        /// </summary>
        /// <param name="triggerPoint"></param>
        /// <param name="lineStart"></param>
        /// <returns></returns>
        private static bool isRemark(SnapshotPoint triggerPoint, SnapshotPoint lineStart) {
            try {
                // check if the line contains a ";" or a "#" before the current point
                for (SnapshotPoint pos = triggerPoint; pos >= lineStart; pos -= 1) {
                    if (AsmTools.Tools.isRemarkChar(pos.GetChar())) {
                        return true;
                    }
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR:AugmentCompletionSession; e={0}", e.Message));
            }
            return false;
        }

        private bool isJump(string previousKeyword) {
            return this._asmDudeTools.isJumpMnenomic(previousKeyword);
        }

        private bool isRegister(string previousKeyword) {
            return AsmTools.Tools.isRegister(previousKeyword);
        }

        private bool isMnemonic(string previousKeyword) {
            return this._asmDudeTools.isMnemonic(previousKeyword);
        }

        private bool isLabel(string previousKeyword) {
            return false;
            //TODO
        }

        /// <summary>
        /// Find the previous keyword (if any) that exists BEFORE the provided triggerPoint, and the provided start.
        /// Eg. qqqq xxxxxx yyyyyyy zzzzzz
        ///     ^             ^
        ///     |begin        |end
        /// the previous keyword is xxxxxx
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        private static string getPreviousKeyword(SnapshotPoint begin, SnapshotPoint end) {

            if (end == 0) return "";
            
            // find the end of previous keyword
            SnapshotPoint endPrevious = begin;
            SnapshotPoint pos = end-1;
            for (; pos >= begin; pos -= 1) {
                if (!AsmTools.Tools.isSeparatorChar(pos.GetChar())) {
                    endPrevious = pos+1;
                    break;
                }
            }
            SnapshotPoint beginPrevious = begin;
            for (; pos >= begin; pos -= 1) {
                if (AsmTools.Tools.isSeparatorChar(pos.GetChar())) {
                    beginPrevious = pos+1;
                    break;
                }
            }
            var applicableTo = end.Snapshot.CreateTrackingSpan(new SnapshotSpan(beginPrevious, endPrevious), SpanTrackingMode.EdgeInclusive);
            string previousKeyword = applicableTo.GetText(end.Snapshot);
            //Debug.WriteLine(string.Format("INFO: getPreviousKeyword; previousKeyword={0}", previousKeyword));
            return previousKeyword;
        }

        private bool isArchSwitchedOn(string arch) {
            if (false) {
                switch (arch) {
                    case "X86": return this._package.OptionsPageCodeCompletion._x86;
                    case "I686": return this._package.OptionsPageCodeCompletion._i686;
                    case "MMX": return this._package.OptionsPageCodeCompletion._mmx;
                    case "SSE": return this._package.OptionsPageCodeCompletion._sse;
                    case "SSE2": return this._package.OptionsPageCodeCompletion._sse2;
                    case "SSE3": return this._package.OptionsPageCodeCompletion._sse3;
                    case "SSSE3": return this._package.OptionsPageCodeCompletion._ssse3;
                    case "SSE4.1": return this._package.OptionsPageCodeCompletion._sse41;
                    case "SSE4.2": return this._package.OptionsPageCodeCompletion._sse42;
                    case "AVX": return this._package.OptionsPageCodeCompletion._avx;
                    case "AVX2": return this._package.OptionsPageCodeCompletion._avx2;
                    case "KNC": return this._package.OptionsPageCodeCompletion._knc;
                    case null:
                    case "": return true;
                    default:
                        Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:isArchSwitchedOn; unsupported arch {0}", arch));
                        return true;
                }
            } else {
                switch (arch) {
                    case "X86": return Settings.Default.CodeCompletion_x86;
                    case "I686": return Settings.Default.CodeCompletion_x86;
                    case "MMX": return Settings.Default.CodeCompletion_mmx;
                    case "SSE": return Settings.Default.CodeCompletion_sse;
                    case "SSE2": return Settings.Default.CodeCompletion_sse2;
                    case "SSE3": return Settings.Default.CodeCompletion_sse3;
                    case "SSSE3": return Settings.Default.CodeCompletion_ssse3;
                    case "SSE4.1": return Settings.Default.CodeCompletion_sse41;
                    case "SSE4.2": return Settings.Default.CodeCompletion_sse42;
                    case "AVX": return Settings.Default.CodeCompletion_avx;
                    case "AVX2": return Settings.Default.CodeCompletion_avx2;
                    case "KNC": return Settings.Default.CodeCompletion_knc;
                    case null:
                    case "": return true;
                    default:
                        Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:isArchSwitchedOn; unsupported arch {0}", arch));
                        return true;
                }
            }
        }

        private void loadIcons() {
            Uri uri = null;
            string installPath = AsmDudeToolsStatic.getInstallPath();
            try {
                uri = new Uri(installPath + "Resources/images/icon-R-blue.png");
                this._icons[TokenType.Register] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "Resources/images/icon-M.png");
                this._icons[TokenType.Mnemonic] = AsmDudeToolsStatic.bitmapFromUri(uri);
                this._icons[TokenType.Jump] = this._icons[TokenType.Mnemonic];
            } catch (FileNotFoundException) {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "Resources/images/icon-question.png");
                this._icons[TokenType.Misc] = AsmDudeToolsStatic.bitmapFromUri(uri);
                this._icons[TokenType.Directive] = this._icons[TokenType.Misc];
            } catch (FileNotFoundException) {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try {
                uri = new Uri(installPath + "Resources/images/icon-L.png");
                this._icons[TokenType.Label] = AsmDudeToolsStatic.bitmapFromUri(uri);
            } catch (FileNotFoundException) {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
        }
        #endregion
    }
}

