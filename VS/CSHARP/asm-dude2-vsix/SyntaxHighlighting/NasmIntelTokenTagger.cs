// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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

namespace AsmDude2
{
    using System;
    using System.Collections.Generic;
    using AsmDude2.SyntaxHighlighting;
    using AsmDude2.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class NasmIntelTokenTagger : ITagger<AsmTokenTag>
    {
        private readonly AsmDude2Tools asmDudeTools_ = null;

        private readonly AsmTokenTag mnemonic_;
        private readonly AsmTokenTag register_;
        private readonly AsmTokenTag remark_;
        private readonly AsmTokenTag directive_;
        private readonly AsmTokenTag constant_;
        private readonly AsmTokenTag jump_;
        private readonly AsmTokenTag label_;
        private readonly AsmTokenTag labelDef_;
        private readonly AsmTokenTag misc_;
        private readonly AsmTokenTag userDefined1_;
        private readonly AsmTokenTag userDefined2_;
        private readonly AsmTokenTag userDefined3_;
        private readonly AsmTokenTag UNKNOWN_;

        internal NasmIntelTokenTagger(ITextBuffer buffer)
        {
            this.asmDudeTools_ = AsmDude2Tools.Create(AsmDudeToolsStatic.Get_Install_Path(), null);

            this.mnemonic_ = new AsmTokenTag(AsmTokenType.Mnemonic);
            this.register_ = new AsmTokenTag(AsmTokenType.Register);
            this.remark_ = new AsmTokenTag(AsmTokenType.Remark);
            this.directive_ = new AsmTokenTag(AsmTokenType.Directive);
            this.constant_ = new AsmTokenTag(AsmTokenType.Constant);
            this.jump_ = new AsmTokenTag(AsmTokenType.Jump);
            this.label_ = new AsmTokenTag(AsmTokenType.Label);
            this.labelDef_ = new AsmTokenTag(AsmTokenType.LabelDef);
            this.misc_ = new AsmTokenTag(AsmTokenType.Misc);
            this.userDefined1_ = new AsmTokenTag(AsmTokenType.UserDefined1);
            this.userDefined2_ = new AsmTokenTag(AsmTokenType.UserDefined2);
            this.userDefined3_ = new AsmTokenTag(AsmTokenType.UserDefined3);
            this.UNKNOWN_ = new AsmTokenTag(AsmTokenType.UNKNOWN);
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<AsmTokenTag>.TagsChanged
        {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            DateTime time1 = DateTime.Now;

            if (spans.Count == 0)
            { //there is no content in the buffer
                yield break;
            }

            foreach (SnapshotSpan curSpan in spans)
            {
                string content = curSpan.GetText();
                string[] lines = content.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length > 0)
                {
                    int startLineNumber = curSpan.Start.GetContainingLineNumber();
                    var s = curSpan.Snapshot;
                    int offset = curSpan.Start.GetContainingLine().Start.Position;

                    for (int i = 0; i < lines.Length; ++i)
                    {
                        string lineStr = lines[i];
                        if (lineStr.Length > 0)
                        {
                            int lineNumber = i + startLineNumber;
                            foreach ((int beginPos, int endPos, AsmTokenType type) in Parse.ParseNasmIntel(lineStr, this.asmDudeTools_))
                            {
                                int length = endPos - beginPos;
                                int beginPosOverall = beginPos + offset;
                                yield return new TagSpan<AsmTokenTag>(new SnapshotSpan(s, new Span(beginPosOverall, length)), this.GetAsmTokenTag(type));
                            }
                        }
                        offset += lineStr.Length;
                    }
                }
            }

            AsmDudeToolsStatic.Print_Speed_Warning(time1, "NasmIntelTokenTagger");
        }

        private AsmTokenTag GetAsmTokenTag(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Mnemonic:
                case AsmTokenType.MnemonicOff: return this.mnemonic_;
                case AsmTokenType.Register: return this.register_;
                case AsmTokenType.Remark: return this.remark_;
                case AsmTokenType.Directive: return this.directive_;
                case AsmTokenType.Constant: return this.constant_;
                case AsmTokenType.Jump: return this.jump_;
                case AsmTokenType.Label: return this.label_;
                case AsmTokenType.LabelDef: return this.labelDef_;
                case AsmTokenType.Misc: return this.misc_;
                case AsmTokenType.UserDefined1: return this.userDefined1_;
                case AsmTokenType.UserDefined2: return this.userDefined2_;
                case AsmTokenType.UserDefined3: return this.userDefined3_;
                case AsmTokenType.UNKNOWN: return this.UNKNOWN_;
                default: return this.UNKNOWN_;
            }
        }

        public static SnapshotSpan New_Span((int beginPos, int length, AsmTokenType _) pos, int offset, SnapshotSpan lineSnapShot)
        {
            return new SnapshotSpan(lineSnapShot.Snapshot, new Span(pos.beginPos + offset, pos.length - pos.beginPos));
        }
    }
}
