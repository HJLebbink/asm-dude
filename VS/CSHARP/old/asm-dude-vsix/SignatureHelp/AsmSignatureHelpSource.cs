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

namespace AsmDude.SignatureHelp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;

    internal class AsmSignatureHelpSource : ISignatureHelpSource
    {
        private readonly ITextBuffer buffer_;
        private readonly MnemonicStore store_;

        public AsmSignatureHelpSource(ITextBuffer buffer)
        {
            AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource:constructor");
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.store_ = AsmDudeTools.Instance.Mnemonic_Store;
        }

        /// <summary>
        /// Constrain the list of signatures given: 1) the currently operands provided by the user; and 2) the selected architectures
        /// </summary>
        /// <param name="data"></param>
        /// <param name="operands"></param>
        /// <returns></returns>
        public static IEnumerable<AsmSignatureElement> Constrain_Signatures(
                IEnumerable<AsmSignatureElement> data,
                IList<Operand> operands,
                ISet<Arch> selectedArchitectures)
        {
            foreach (AsmSignatureElement asmSignatureElement in data)
            {
                bool allowed = true;

                //1] constrain on architecture
                if (!asmSignatureElement.Is_Allowed(selectedArchitectures))
                {
                    allowed = false;
                }

                //2] constrain on operands
                if (allowed)
                {
                    if ((operands == null) || (operands.Count == 0))
                    {
                        // do nothing
                    }
                    else
                    {
                        for (int i = 0; i < operands.Count; ++i)
                        {
                            Operand operand = operands[i];
                            if (operand != null)
                            {
                                if (!asmSignatureElement.Is_Allowed(operand, i))
                                {
                                    allowed = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (allowed)
                {
                    yield return asmSignatureElement;
                }
            }
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures)
        {
            AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: AugmentSignatureHelpSession");

            //if (true) return;
            if (!Settings.Default.SignatureHelp_On)
            {
                return;
            }

            try
            {
                DateTime time1 = DateTime.Now;
                ITextSnapshot snapshot = this.buffer_.CurrentSnapshot;
                int position = session.GetTriggerPoint(this.buffer_).GetPosition(snapshot);
                ITrackingSpan applicableToSpan = this.buffer_.CurrentSnapshot.CreateTrackingSpan(new Span(position, 0), SpanTrackingMode.EdgeInclusive, 0);

                ITextSnapshotLine line = snapshot.GetLineFromPosition(position);
                string lineStr = line.GetText();
                //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: AugmentSignatureHelpSession: lineStr=" + lineStr+ "; positionInLine=" + positionInLine);

                (string label, Mnemonic mnemonic, string[] args, string remark) t = AsmSourceTools.ParseLine(lineStr);
                IList<Operand> operands = AsmSourceTools.MakeOperands(t.args);
                Mnemonic mnemonic = t.mnemonic;

                ISet<Arch> selectedArchitectures = AsmDudeToolsStatic.Get_Arch_Swithed_On();
                //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: AugmentSignatureHelpSession: selected architectures=" + ArchTools.ToString(selectedArchitectures));

                foreach (AsmSignatureElement se in Constrain_Signatures(this.store_.GetSignatures(mnemonic), operands, selectedArchitectures))
                {
                    signatures.Add(this.Create_Signature(this.buffer_, se, applicableToSpan));
                }
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "Signature Help");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentSignatureHelpSession; e={1}", this.ToString(), e.ToString()));
            }
        }

        public ISignature GetBestMatch(ISignatureHelpSession session)
        {
            //NOT USED!!

            AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: GetBestMatch");

            if (session.Signatures.Count > 0)
            {
                ITrackingSpan applicableToSpan = session.Signatures[0].ApplicableToSpan;
                string text_upcase = applicableToSpan.GetText(applicableToSpan.TextBuffer.CurrentSnapshot).Trim().ToUpperInvariant();

                AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: GetBestMatch: session.Signatures.Count=" + session.Signatures.Count);
                /*
                if (text.Equals("ADD")) {
                    return session.Signatures[0];
                } else if (text.Equals("AND")) {
                    return session.Signatures[0];
                }
                */
            }
            return null;
        }

        private AsmSignature Create_Signature(ITextBuffer textBuffer, AsmSignatureElement signatureElement, ITrackingSpan span)
        {
            int nOperands = signatureElement.Operands.Count;
            Span[] locus = new Span[nOperands];

            StringBuilder sb = new StringBuilder();
            sb.Append(signatureElement.mnemonic.ToString());
            sb.Append(' ');
            //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: createSignature: sb=" + sb.ToString());

            for (int i = 0; i < nOperands; ++i)
            {
                int locusStart = sb.Length;
                sb.Append(signatureElement.Get_Operand_Doc(i));
                //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: createSignature: i="+i+"; sb=" + sb.ToString());
                locus[i] = new Span(locusStart, sb.Length - locusStart);
                if (i < nOperands - 1)
                {
                    sb.Append(", ");
                }
            }

            sb.Append(ArchTools.ToString(signatureElement.Arch));
            AsmSignature sig = new AsmSignature(textBuffer, sb.ToString(), signatureElement.Documentation, null);
            textBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(sig.OnSubjectBufferChanged);

            IList<IParameter> paramList = new List<IParameter>();
            for (int i = 0; i < nOperands; ++i)
            {
                string documentation = AsmSignatureElement.Make_Doc(signatureElement.Operands[i]);
                string operandName = signatureElement.Get_Operand_Str(i);
                paramList.Add(new AsmParameter(documentation, locus[i], operandName, sig));
            }

            sig.Parameters = new ReadOnlyCollection<IParameter>(paramList);
            sig.ApplicableToSpan = span;
            sig.Compute_Current_Parameter();
            return sig;
        }

        public void Dispose() { }
    }
}