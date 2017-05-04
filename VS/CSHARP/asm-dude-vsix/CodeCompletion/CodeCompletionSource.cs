// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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
using System.IO;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Windows.Media;

using AsmTools;
using AsmDude.Tools;
using AsmDude.SignatureHelp;
using System.Text;
using AsmSimZ3.Mnemonics_ng;
using AsmSimZ3;

namespace AsmDude
{
    public sealed class CompletionComparer : IComparer<Completion>
    {
        public int Compare(Completion x, Completion y)
        {
            return x.InsertionText.CompareTo(y.InsertionText);
        }
    }

    public sealed class CodeCompletionSource : ICompletionSource
    {
        private readonly ITextBuffer _buffer;
        private readonly ILabelGraph _labelGraph;
        private readonly IDictionary<AsmTokenType, ImageSource> _icons;
        private readonly AsmDudeTools _asmDudeTools;
        private readonly AsmSimulator _asmSimulator;
        private bool _disposed = false;

        public CodeCompletionSource(ITextBuffer buffer, ILabelGraph labelGraph, AsmSimulator asmSimulator)
        {
            this._buffer = buffer;
            this._labelGraph = labelGraph;
            this._icons = new Dictionary<AsmTokenType, ImageSource>();
            this._asmDudeTools = AsmDudeTools.Instance;
            this._asmSimulator = asmSimulator;
            Load_Icons();
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:AugmentCompletionSession", this.ToString()));

            if (this._disposed) return;
            if (!Settings.Default.CodeCompletion_On) return;

            try
            {
                DateTime time1 = DateTime.Now;
                ITextSnapshot snapshot = this._buffer.CurrentSnapshot;
                SnapshotPoint triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);
                if (triggerPoint == null)
                {
                    return;
                }
                ITextSnapshotLine line = triggerPoint.GetContainingLine();

                //1] check if current position is in a remark; if we are in a remark, no code completion
                #region
                if (triggerPoint.Position > 1)
                {
                    char currentTypedChar = (triggerPoint - 1).GetChar();
                    //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession: current char = "+ currentTypedChar);
                    if (!currentTypedChar.Equals('#'))
                    { //TODO UGLY since the user can configure this starting character
                        int pos = triggerPoint.Position - line.Start;
                        if (AsmSourceTools.IsInRemark(pos, line.GetText()))
                        {
                            //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession: currently in a remark section");
                            return;
                        }
                        else
                        {
                            // AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession: not in a remark section");
                        }
                    }
                }
                #endregion

                //2] find the start of the current keyword
                #region
                SnapshotPoint start = triggerPoint;
                while ((start > line.Start) && !AsmTools.AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
                {
                    start -= 1;
                }
                #endregion

                //3] get the word that is currently being typed
                #region
                ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
                string partialKeyword = applicableTo.GetText(snapshot);
                bool useCapitals = AsmDudeToolsStatic.Is_All_Upper(partialKeyword);

                SortedSet<Completion> completions = null;

                string lineStr = line.GetText();
                var t = AsmSourceTools.ParseLine(lineStr);
                Mnemonic mnemonic = t.mnemonic;

                //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession; lineStr="+ lineStr+ "; t.Item1="+t.Item1);

                string previousKeyword = AsmDudeToolsStatic.Get_Previous_Keyword(line.Start, start).ToUpper();

                if (mnemonic == Mnemonic.UNKNOWN)
                {
                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession; lineStr=" + lineStr + "; previousKeyword=" + previousKeyword);

                    if (previousKeyword.Equals("INVOKE")) //TODO INVOKE is a MASM keyword not a NASM one...
                    {
                        // Suggest a label
                        completions = Label_Completions();
                    }
                    else
                    {
                        ISet<AsmTokenType> selected = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                        completions = Selected_Completions(useCapitals, selected);
                    }
                }
                else
                { // the current line contains a mnemonic
                    //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession; mnemonic=" + mnemonic+ "; previousKeyword="+ previousKeyword);

                    if (AsmSourceTools.IsJump(AsmSourceTools.ParseMnemonic(previousKeyword)))
                    {
                        //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession; previous keyword is a jump mnemonic");
                        // previous keyword is jump (or call) mnemonic. Suggest "SHORT" or a label
                        completions = Label_Completions();
                        completions.Add(new Completion("SHORT", (useCapitals) ? "SHORT" : "short", null, this._icons[AsmTokenType.Misc], ""));
                        completions.Add(new Completion("NEAR", (useCapitals) ? "NEAR" : "near", null, this._icons[AsmTokenType.Misc], ""));
                    }
                    else if (previousKeyword.Equals("SHORT") || previousKeyword.Equals("NEAR"))
                    {
                        // Suggest a label
                        completions = Label_Completions();
                    }
                    else
                    {
                        IList<Operand> operands = AsmSourceTools.MakeOperands(t.args);
                        ISet<AsmSignatureEnum> allowed = new HashSet<AsmSignatureEnum>();
                        int commaCount = AsmSignature.Count_Commas(lineStr);
                        IEnumerable<AsmSignatureElement> allSignatures = this._asmDudeTools.Mnemonic_Store.GetSignatures(mnemonic);

                        ISet<Arch> selectedArchitectures = AsmDudeToolsStatic.Get_Arch_Swithed_On();
                        foreach (AsmSignatureElement se in AsmSignatureHelpSource.Constrain_Signatures(allSignatures, operands, selectedArchitectures))
                        {
                            if (commaCount < se.Operands.Count)
                            {
                                foreach (AsmSignatureEnum s in se.Operands[commaCount])
                                {
                                    allowed.Add(s);
                                }
                            }
                        }
                        completions = this.Mnemonic_Operand_Completions(useCapitals, allowed, line.LineNumber);
                    }
                }
                //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:AugmentCompletionSession; nCompletions=" + completions.Count);
                #endregion

                completionSets.Add(new CompletionSet("Tokens", "Tokens", applicableTo, completions, Enumerable.Empty<Completion>()));

                AsmDudeToolsStatic.Print_Speed_Warning(time1, "Code Completion");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:AugmentCompletionSession; e={1}", ToString(), e.ToString()));
            }
        }

        public void Dispose()
        {
            if (!this._disposed)
            {
                GC.SuppressFinalize(this);
                this._disposed = true;
            }
        }

        #region Private Methods
        private SortedSet<Completion> Mnemonic_Operand_Completions(bool useCapitals, ISet<AsmSignatureEnum> allowedOperands, int lineNumber)
        {
            bool asmSimulator_Enabled = this._asmSimulator.Is_Enabled;

            SortedSet<Completion> completions = new SortedSet<Completion>(new CompletionComparer());
            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                Arch arch = this._asmDudeTools.Get_Architecture(keyword);
                AsmTokenType type = this._asmDudeTools.Get_Token_Type(keyword);

                bool selected = AsmDudeToolsStatic.Is_Arch_Switched_On(arch);
                //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected +"; arch="+arch);

                string additionalInfo = null;

                if (selected)
                {
                    switch (type)
                    {
                        case AsmTokenType.Register:
                            {
                                //AsmDudeToolsStatic.Output("INFO: CodeCompletionSource:Mnemonic_Operand_Completions; rn=" + keyword);
                                Rn regName = RegisterTools.ParseRn(keyword);
                                if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
                                {
                                    if (asmSimulator_Enabled && this._asmSimulator.Tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(regName))) {
                                        State2 state = this._asmSimulator.Get_State_After(lineNumber, false);
                                        Tv5[] content = state.GetTv5Array(regName);
                                        if (state != null)
                                        {
                                            additionalInfo = ToolsZ3.ToStringHex(content) + " = " + ToolsZ3.ToStringBin(content);
                                        }
                                        AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:Mnemonic_Operand_Completions; register " + keyword + " is selected and has value " + additionalInfo);
                                    }
                                }
                                else
                                {
                                    selected = false;
                                }
                                break;
                            }
                        case AsmTokenType.Misc:
                            {
                                if (AsmSignatureTools.Is_Allowed_Misc(keyword, allowedOperands))
                                {
                                    //AsmDudeToolsStatic.Output(string.Format("INFO: AsmCompletionSource:mnemonicOperandCompletions; rn="+ keyword + " is selected"));
                                }
                                else
                                {
                                    selected = false;
                                }
                                break;
                            }
                        default:
                            {
                                selected = false;
                                break;
                            }
                    }
                }
                if (selected)
                {
                    //AsmDudeToolsStatic.Output("INFO: AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = (useCapitals) ? keyword : keyword.ToLower();
                    string archStr = (arch == Arch.NONE) ? "" : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                    descriptionStr = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                    String displayText = keyword + archStr + descriptionStr;
                    //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                    this._icons.TryGetValue(type, out var imageSource);
                    completions.Add(new Completion(displayText, insertionText, additionalInfo, imageSource, ""));
                }
            }
            return completions;
        }

        private SortedSet<Completion> Label_Completions()
        {
            SortedSet<Completion> completions = new SortedSet<Completion>(new CompletionComparer());
            ImageSource imageSource = this._icons[AsmTokenType.Label];
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            SortedDictionary<string, string> labels = this._labelGraph.Get_Label_Descriptions;
            foreach (KeyValuePair<string, string> entry in labels)
            {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                string displayText = entry.Key + " - " + entry.Value;
                string insertionText = AsmDudeToolsStatic.Retrieve_Regular_Label(entry.Key, usedAssember);
                completions.Add(new Completion(displayText, insertionText, null, imageSource, ""));
            }
            return completions;
        }

        private IEnumerable<Mnemonic> Get_Allowed_Mnemonics(ISet<Arch> selectedArchitectures)
        {
            MnemonicStore store = this._asmDudeTools.Mnemonic_Store;
            foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
            {
                foreach (Arch a in store.GetArch(mnemonic))
                {
                    if (selectedArchitectures.Contains(a))
                    {
                        yield return mnemonic;
                        break; // leave the foreach Arch loop
                    }
                }
            }
        }

        private SortedSet<Completion> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes)
        {
            SortedSet<Completion> completions = new SortedSet<Completion>(new CompletionComparer());

            //Add the completions of AsmDude directives (such as code folding directives)
            #region 
            if (Settings.Default.CodeFolding_On)
            {
                this._icons.TryGetValue(AsmTokenType.Directive, out var imageSource);
                {
                    string insertionText = Settings.Default.CodeFolding_BeginTag;     //the characters that start the outlining region
                    string description = insertionText + " - keyword to start code folding";
                    completions.Add(new Completion(description, insertionText, null, imageSource, ""));
                }
                {
                    string insertionText = Settings.Default.CodeFolding_EndTag;       //the characters that end the outlining region
                    string description = insertionText + " - keyword to end code folding";
                    completions.Add(new Completion(description, insertionText, null, imageSource, ""));
                }
            }
            #endregion
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            #region

            if (selectedTypes.Contains(AsmTokenType.Mnemonic))
            {
                ISet<Arch> selectedArchs = AsmDudeToolsStatic.Get_Arch_Swithed_On();
                foreach (Mnemonic mnemonic in Get_Allowed_Mnemonics(selectedArchs))
                {
                    string keyword = mnemonic.ToString();

                    string insertionText = (useCapitals) ? keyword : keyword.ToLower();
                    string archStr = ArchTools.ToString(this._asmDudeTools.Mnemonic_Store.GetArch(mnemonic));
                    string descriptionStr = this._asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
                    descriptionStr = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                    String displayText = keyword + archStr + descriptionStr;
                    //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                    this._icons.TryGetValue(AsmTokenType.Mnemonic, out var imageSource);
                    completions.Add(new Completion(displayText, insertionText, null, imageSource, ""));
                }
            }

            //Add the completions that are defined in the xml file
            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this._asmDudeTools.Get_Token_Type(keyword);
                if (selectedTypes.Contains(type))
                {
                    Arch arch = Arch.NONE;
                    bool selected = true;

                    if (type == AsmTokenType.Directive)
                    {
                        AssemblerEnum assembler = this._asmDudeTools.Get_Assembler(keyword);
                        if (assembler.HasFlag(AssemblerEnum.MASM))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.MASM)) selected = false;
                        }
                        else if (assembler.HasFlag(AssemblerEnum.NASM))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.NASM)) selected = false;
                        }
                    }
                    else
                    {
                        arch = this._asmDudeTools.Get_Architecture(keyword);
                        selected = AsmDudeToolsStatic.Is_Arch_Switched_On(arch);
                    }

                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Selected_Completions; keyword=" + keyword + "; arch=" + arch + "; selected=" + selected);

                    if (selected)
                    {
                        //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");

                        // by default, the entry.Key is with capitals
                        string insertionText = (useCapitals) ? keyword : keyword.ToLower();
                        string archStr = (arch == Arch.NONE) ? "" : " [" + ArchTools.ToString(arch) + "]";
                        string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                        descriptionStr = (descriptionStr.Length == 0) ? "" : " - " + descriptionStr;
                        String displayText = keyword + archStr + descriptionStr;
                        //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                        this._icons.TryGetValue(type, out var imageSource);
                        completions.Add(new Completion(displayText, insertionText, null, imageSource, ""));
                    }
                }
            }
            #endregion

            return completions;
        }

        private void Load_Icons()
        {
            Uri uri = null;
            string installPath = AsmDudeToolsStatic.Get_Install_Path();
            try
            {
                uri = new Uri(installPath + "Resources/images/icon-R-blue.png");
                this._icons[AsmTokenType.Register] = AsmDudeToolsStatic.Bitmap_From_Uri(uri);
            }
            catch (FileNotFoundException)
            {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try
            {
                uri = new Uri(installPath + "Resources/images/icon-M.png");
                this._icons[AsmTokenType.Mnemonic] = AsmDudeToolsStatic.Bitmap_From_Uri(uri);
                this._icons[AsmTokenType.Jump] = this._icons[AsmTokenType.Mnemonic];
            }
            catch (FileNotFoundException)
            {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try
            {
                uri = new Uri(installPath + "Resources/images/icon-question.png");
                this._icons[AsmTokenType.Misc] = AsmDudeToolsStatic.Bitmap_From_Uri(uri);
                this._icons[AsmTokenType.Directive] = this._icons[AsmTokenType.Misc];
            }
            catch (FileNotFoundException)
            {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
            try
            {
                uri = new Uri(installPath + "Resources/images/icon-L.png");
                this._icons[AsmTokenType.Label] = AsmDudeToolsStatic.Bitmap_From_Uri(uri);
            }
            catch (FileNotFoundException)
            {
                //MessageBox.Show("ERROR: AsmCompletionSource: could not find file \"" + uri.AbsolutePath + "\".");
            }
        }
        #endregion
    }
}