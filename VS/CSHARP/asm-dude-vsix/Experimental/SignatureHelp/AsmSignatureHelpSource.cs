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
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using AsmDude.Tools;
using Microsoft.VisualStudio.Text.Operations;
using AsmTools;

namespace AsmDude.SignatureHelp {


    internal class AsmSignatureHelpSource : ISignatureHelpSource {
        private readonly ITextBuffer _textBuffer;
        private readonly SignatureStore _store;

        public AsmSignatureHelpSource(ITextBuffer textBuffer) {
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource:constructor");

            this._textBuffer = textBuffer;
            this._store = AsmDudeTools.Instance.signatureStore;
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource:constructor: "+this._store.ToString());
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures) {
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: AugmentSignatureHelpSession");

            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            int position = session.GetTriggerPoint(_textBuffer).GetPosition(snapshot);
            ITrackingSpan applicableToSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(new Span(position, 0), SpanTrackingMode.EdgeInclusive, 0);

            this.fill(snapshot.GetLineFromPosition(position), position, signatures, applicableToSpan);
        }

        public ISignature GetBestMatch(ISignatureHelpSession session) {
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: GetBestMatch");

            if (session.Signatures.Count > 0) {
                ITrackingSpan applicableToSpan = session.Signatures[0].ApplicableToSpan;
                string text = applicableToSpan.GetText(applicableToSpan.TextBuffer.CurrentSnapshot).Trim().ToUpper();

                AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: GetBestMatch: session.Signatures.Count=" + session.Signatures.Count);

                if (text.Equals("ADD")) {
                    return session.Signatures[0];
                } else if (text.Equals("AND")) {
                    return session.Signatures[0];
                }
            }
            return null;
        }

        private void fill(ITextSnapshotLine line, int position, IList<ISignature> signatures, ITrackingSpan applicableToSpan) {

            string lineStr = line.GetText();
            int positionInLine = position - line.Start;
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: lineStr=" + lineStr+ "; positionInLine=" + positionInLine);

            var t = AsmSourceTools.parseLine(lineStr);
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: Mnemonic=" + t.Item2 + "; args=" + string.Join(",", t.Item3));


            Operand[] operands = new Operand[t.Item3.Length];
            for (int i = 0; i < t.Item3.Length; ++i) {
                string opStr = t.Item3[i];
                if (opStr.Length > 0) {
                    operands[i] = new Operand(opStr);
                }
                //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: args["+i+"]=" + operands[i]);
            }

            foreach (SignatureElement se in this._store.get(t.Item2)) {
                bool allowed = true;
                for (int i = 0; i < operands.Length; ++i) {
                    if (!se.isAllowed(operands[i], i)) {
                        //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: i="+i+"; Operand=" + operands[i] + " is not allowed. se="+se);
                        allowed = false;
                        break;
                    }
                }
                if (allowed) {
                    string description = AsmDudeTools.Instance.getDescription(se.mnemonic.ToString());
                    signatures.Add(this.createSignature(_textBuffer, se, description, applicableToSpan));
                }
            }
        }

        private AsmSignature createSignature(ITextBuffer textBuffer, SignatureElement signatureElement, string methodDoc, ITrackingSpan span) {
            string methodSig = signatureElement.ToString();

            AsmSignature sig = new AsmSignature(textBuffer, methodSig, methodDoc, null);
            textBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(sig.OnSubjectBufferChanged);

            //find the parameters in the method signature (expect OPCODE one, two, three)
            string[] pars = methodSig.Split(new char[] { ',', ' ' });
            List<IParameter> paramList = new List<IParameter>();

            int locusSearchStart = 0;
            for (int i = 1; i < pars.Length; i++) {
                string param = pars[i].Trim();

                if (string.IsNullOrEmpty(param)) {
                    continue;
                }
                //find where this parameter is located in the method signature
                int locusStart = methodSig.IndexOf(param, locusSearchStart);
                if (locusStart >= 0) {
                    Span locus = new Span(locusStart, param.Length);
                    locusSearchStart = locusStart + param.Length;
                    paramList.Add(new AsmParameter(SignatureElement.getDoc(signatureElement.operands[i-1]), locus, param, sig));
                }
            }

            sig.Parameters = new ReadOnlyCollection<IParameter>(paramList);
            sig.ApplicableToSpan = span;
            sig.computeCurrentParameter();
            return sig;
        }

        private bool _isDisposed;
        public void Dispose() {
            if (!_isDisposed) {
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }
    }
}