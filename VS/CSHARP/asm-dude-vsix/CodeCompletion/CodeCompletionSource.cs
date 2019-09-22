// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Media;
    using AsmDude.SignatureHelp;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;

    public sealed class CompletionComparer : IComparer<Completion>
    {
        public int Compare(Completion x, Completion y)
        {
            return x.InsertionText.CompareTo(y.InsertionText);
        }
    }

    public sealed class CodeCompletionSource : ICompletionSource
    {
        private const int MAX_LENGTH_DESCR_TEXT = 120;

        private readonly ITextBuffer _buffer;
        private readonly LabelGraph _labelGraph;
        private readonly IDictionary<AsmTokenType, ImageSource> _icons;
        private readonly AsmDudeTools _asmDudeTools;
        private readonly AsmSimulator _asmSimulator;

        public CodeCompletionSource(ITextBuffer buffer, LabelGraph labelGraph, AsmSimulator asmSimulator)
        {
            this._buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this._labelGraph = labelGraph ?? throw new ArgumentNullException(nameof(labelGraph));
            this._icons = new Dictionary<AsmTokenType, ImageSource>();
            this._asmDudeTools = AsmDudeTools.Instance;
            this._asmSimulator = asmSimulator ?? throw new ArgumentNullException(nameof(asmSimulator));
            this.Load_Icons();
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            try
            {
                //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:AugmentCompletionSession", this.ToString()));

                if (!Settings.Default.CodeCompletion_On)
                {
                    return;
                }

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
                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession: current char = "+ currentTypedChar);
                    if (!currentTypedChar.Equals('#'))
                    { //TODO UGLY since the user can configure this starting character
                        int pos = triggerPoint.Position - line.Start;
                        if (AsmSourceTools.IsInRemark(pos, line.GetText()))
                        {
                            //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession: currently in a remark section");
                            return;
                        }
                        else
                        {
                            // AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession: not in a remark section");
                        }
                    }
                }
                #endregion

                //2] find the start of the current keyword
                #region
                SnapshotPoint start = triggerPoint;
                while ((start > line.Start) && !AsmSourceTools.IsSeparatorChar((start - 1).GetChar()))
                {
                    start -= 1;
                }
                #endregion

                //3] get the word that is currently being typed
                #region
                ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);
                string partialKeyword = applicableTo.GetText(snapshot);
                bool useCapitals = AsmDudeToolsStatic.Is_All_Upper(partialKeyword);

                string lineStr = line.GetText();
                (string Label, Mnemonic Mnemonic, string[] Args, string Remark) t = AsmSourceTools.ParseLine(lineStr);
                Mnemonic mnemonic = t.Mnemonic;
                string previousKeyword = AsmDudeToolsStatic.Get_Previous_Keyword(line.Start, start).ToUpper();

                //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:AugmentCompletionSession. lineStr=\"{1}\"; previousKeyword=\"{2}\"", this.ToString(), lineStr, previousKeyword));

                if (mnemonic == Mnemonic.NONE)
                {
                    if (previousKeyword.Equals("INVOKE")) //TODO INVOKE is a MASM keyword not a NASM one...
                    {
                        // Suggest a label
                        IEnumerable<Completion> completions = this.Label_Completions(useCapitals, false);
                        if (completions.Any())
                        {
                            completionSets.Add(new CompletionSet("Labels", "Labels", applicableTo, completions, Enumerable.Empty<Completion>()));
                        }
                    }
                    else
                    {
                        {
                            ISet<AsmTokenType> selected1 = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                            IEnumerable<Completion> completions1 = this.Selected_Completions(useCapitals, selected1, true);
                            if (completions1.Any())
                            {
                                completionSets.Add(new CompletionSet("All", "All", applicableTo, completions1, Enumerable.Empty<Completion>()));
                            }
                        }
                        if (false)
                        {
                            ISet<AsmTokenType> selected2 = new HashSet<AsmTokenType> { AsmTokenType.Jump, AsmTokenType.Mnemonic };
                            IEnumerable<Completion> completions2 = this.Selected_Completions(useCapitals, selected2, false);
                            if (completions2.Any())
                            {
                                completionSets.Add(new CompletionSet("Instr", "Instr", applicableTo, completions2, Enumerable.Empty<Completion>()));
                            }
                        }
                        if (false)
                        {
                            ISet<AsmTokenType> selected3 = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Misc };
                            IEnumerable<Completion> completions3 = this.Selected_Completions(useCapitals, selected3, true);
                            if (completions3.Any())
                            {
                                completionSets.Add(new CompletionSet("Directive", "Directive", applicableTo, completions3, Enumerable.Empty<Completion>()));
                            }
                        }
                    }
                }
                else
                { // the current line contains a mnemonic
                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession; mnemonic=" + mnemonic+ "; previousKeyword="+ previousKeyword);

                    if (AsmSourceTools.IsJump(AsmSourceTools.ParseMnemonic(previousKeyword, true)))
                    {
                        //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:AugmentCompletionSession; previous keyword is a jump mnemonic");
                        // previous keyword is jump (or call) mnemonic. Suggest "SHORT" or a label
                        IEnumerable<Completion> completions = this.Label_Completions(useCapitals, true);
                        if (completions.Any())
                        {
                            completionSets.Add(new CompletionSet("Labels", "Labels", applicableTo, completions, Enumerable.Empty<Completion>()));
                        }
                    }
                    else if (previousKeyword.Equals("SHORT") || previousKeyword.Equals("NEAR"))
                    {
                        // Suggest a label
                        IEnumerable<Completion> completions = this.Label_Completions(useCapitals, false);
                        if (completions.Any())
                        {
                            completionSets.Add(new CompletionSet("Labels", "Labels", applicableTo, completions, Enumerable.Empty<Completion>()));
                        }
                    }
                    else
                    {
                        IList<Operand> operands = AsmSourceTools.MakeOperands(t.Args);
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
                        IEnumerable<Completion> completions = this.Mnemonic_Operand_Completions(useCapitals, allowed, line.LineNumber);
                        if (completions.Any())
                        {
                            completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
                        }
                    }
                }
                #endregion
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "Code Completion");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:AugmentCompletionSession; e={1}", this.ToString(), e.ToString()));
            }
        }

        #region Private Methods
        private IEnumerable<Completion> Mnemonic_Operand_Completions(bool useCapitals, ISet<AsmSignatureEnum> allowedOperands, int lineNumber)
        {
            bool use_AsmSim_In_Code_Completion = this._asmSimulator.Enabled && Settings.Default.AsmSim_Show_Register_In_Code_Completion;
            bool att_Syntax = AsmDudeToolsStatic.Used_Assembler == AssemblerEnum.NASM_ATT;

            SortedSet<Completion> completions = new SortedSet<Completion>(new CompletionComparer());

            foreach (Rn regName in this._asmDudeTools.Get_Allowed_Registers())
            {
                string additionalInfo = null;
                if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
                {
                    string keyword = regName.ToString();
                    if (use_AsmSim_In_Code_Completion && this._asmSimulator.Tools.StateConfig.IsRegOn(RegisterTools.Get64BitsRegister(regName)))
                    {
                        (string value, bool bussy) = this._asmSimulator.Get_Register_Value(regName, lineNumber, true, false, false, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration));
                        if (!bussy)
                        {
                            additionalInfo = value;
                            AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:Mnemonic_Operand_Completions; register " + keyword + " is selected and has value " + additionalInfo);
                        }
                    }

                    if (att_Syntax)
                    {
                        keyword = "%" + keyword;
                    }

                    Arch arch = RegisterTools.GetArch(regName);
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword : keyword.ToLower();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                    descriptionStr = (descriptionStr.Length == 0) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncat(keyword + archStr + descriptionStr);
                    this._icons.TryGetValue(AsmTokenType.Register, out ImageSource imageSource);
                    completions.Add(new Completion(displayText, insertionText, additionalInfo, imageSource, string.Empty));
                }
            }

            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this._asmDudeTools.Get_Token_Type_Intel(keyword);
                Arch arch = this._asmDudeTools.Get_Architecture(keyword);

                string keyword2 = keyword;
                bool selected = true;

                //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Mnemonic_Operand_Completions; keyword=" + keyword +"; selected="+selected +"; arch="+arch);

                string additionalInfo = null;
                switch (type)
                {
                    case AsmTokenType.Misc:
                        {
                            if (!AsmSignatureTools.Is_Allowed_Misc(keyword, allowedOperands))
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
                if (selected)
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmCompletionSource:AugmentCompletionSession: keyword \"" + keyword + "\" is added to the completions list");

                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword2 : keyword2.ToLower();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                    descriptionStr = (descriptionStr.Length == 0) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncat(keyword2 + archStr + descriptionStr);
                    this._icons.TryGetValue(type, out ImageSource imageSource);
                    completions.Add(new Completion(displayText, insertionText, additionalInfo, imageSource, string.Empty));
                }
            }
            return completions;
        }

        private static string Truncat(string text)
        {
            if (text.Length < MAX_LENGTH_DESCR_TEXT)
            {
                return text;
            }

            return text.Substring(0, MAX_LENGTH_DESCR_TEXT) + "...";
        }

        private IEnumerable<Completion> Label_Completions(bool useCapitals, bool addSpecialKeywords)
        {
            if (addSpecialKeywords)
            {
                yield return new Completion("SHORT", useCapitals ? "SHORT" : "short", null, this._icons[AsmTokenType.Misc], string.Empty);
                yield return new Completion("NEAR", useCapitals ? "NEAR" : "near", null, this._icons[AsmTokenType.Misc], string.Empty);
            }

            ImageSource imageSource = this._icons[AsmTokenType.Label];
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            SortedDictionary<string, string> labels = this._labelGraph.Label_Descriptions;
            foreach (KeyValuePair<string, string> entry in labels)
            {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO:{0}:AugmentCompletionSession; label={1}; description={2}", this.ToString(), entry.Key, entry.Value));
                string displayTextFull = entry.Key + " - " + entry.Value;
                string displayText = Truncat(displayTextFull);
                string insertionText = AsmDudeToolsStatic.Retrieve_Regular_Label(entry.Key, usedAssember);
                yield return new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty);
            }
        }

        private IEnumerable<Completion> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
        {
            SortedSet<Completion> completions = new SortedSet<Completion>(new CompletionComparer());

            //Add the completions of AsmDude directives (such as code folding directives)
            #region
            if (addSpecialKeywords && Settings.Default.CodeFolding_On)
            {
                this._icons.TryGetValue(AsmTokenType.Directive, out ImageSource imageSource);
                {
                    string insertionText = Settings.Default.CodeFolding_BeginTag;     //the characters that start the outlining region
                    string displayTextFull = insertionText + " - keyword to start code folding";
                    string displayText = Truncat(displayTextFull);
                    completions.Add(new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty));
                }
                {
                    string insertionText = Settings.Default.CodeFolding_EndTag;       //the characters that end the outlining region
                    string displayTextFull = insertionText + " - keyword to end code folding";
                    string displayText = Truncat(displayTextFull);
                    completions.Add(new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty));
                }
            }
            #endregion
            AssemblerEnum usedAssember = AsmDudeToolsStatic.Used_Assembler;

            #region Add completions

            if (selectedTypes.Contains(AsmTokenType.Mnemonic))
            {
                this._icons.TryGetValue(AsmTokenType.Mnemonic, out ImageSource imageSource);
                foreach (Mnemonic mnemonic in this._asmDudeTools.Get_Allowed_Mnemonics())
                {
                    string keyword = mnemonic.ToString();
                    string description = this._asmDudeTools.Mnemonic_Store.GetSignatures(mnemonic).First().Documentation;
                    string insertionText = useCapitals ? keyword : keyword.ToLower();
                    string archStr = ArchTools.ToString(this._asmDudeTools.Mnemonic_Store.GetArch(mnemonic));
                    string descriptionStr = this._asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
                    descriptionStr = (descriptionStr.Length == 0) ? string.Empty : " - " + descriptionStr;
                    string displayText = Truncat(keyword + archStr + descriptionStr);
                    //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                    completions.Add(new Completion(displayText, insertionText, description, imageSource, string.Empty));
                }
            }

            //Add the completions that are defined in the xml file
            foreach (string keyword in this._asmDudeTools.Get_Keywords())
            {
                AsmTokenType type = this._asmDudeTools.Get_Token_Type_Intel(keyword);
                if (selectedTypes.Contains(type))
                {
                    Arch arch = Arch.ARCH_NONE;
                    bool selected = true;

                    if (type == AsmTokenType.Directive)
                    {
                        AssemblerEnum assembler = this._asmDudeTools.Get_Assembler(keyword);
                        if (assembler.HasFlag(AssemblerEnum.MASM))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.MASM))
                            {
                                selected = false;
                            }
                        }
                        else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.NASM_INTEL))
                            {
                                selected = false;
                            }
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
                        string insertionText = useCapitals ? keyword : keyword.ToLower();
                        string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                        string descriptionStr = this._asmDudeTools.Get_Description(keyword);
                        descriptionStr = (descriptionStr.Length == 0) ? string.Empty : " - " + descriptionStr;
                        string displayTextFull = keyword + archStr + descriptionStr;
                        string displayText = Truncat(displayTextFull);
                        //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                        this._icons.TryGetValue(type, out ImageSource imageSource);
                        completions.Add(new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty));
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

        public void Dispose() { }
        #endregion
    }
}