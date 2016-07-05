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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using AsmDude.Tools;
using Microsoft.VisualStudio.Text.Operations;
using AsmTools;

namespace AsmDude.SignatureHelp {

    //TODO: get the data for code help from http://www.nasm.us/doc/nasmdocb.html

    [Export(typeof(ISignatureHelpSourceProvider))]
    [Name("Signature Help source")]
    [Order(Before = "default")]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    internal class AsmSignatureHelpSourceProvider : ISignatureHelpSourceProvider {

        [Import]
        private ITextStructureNavigatorSelectorService _navigatorService = null;

        public ISignatureHelpSource TryCreateSignatureHelpSource(ITextBuffer textBuffer) {
            return new AsmSignatureHelpSource(textBuffer, _navigatorService.GetTextStructureNavigator(textBuffer));
        }
    }

    internal class AsmParameter : IParameter {
        public AsmParameter(string documentation, Span locus, string name, ISignature signature) {
            Documentation = documentation;
            Locus = locus;
            Name = name;
            Signature = signature;
        }
        public string Documentation { get; private set; }
        public Span Locus { get; private set; }
        public string Name { get; private set; }
        public ISignature Signature { get; private set; }
        public Span PrettyPrintedLocus { get; private set; }
    }

    internal class AsmSignature : ISignature {
        private readonly ITextBuffer _subjectBuffer;

        private IParameter _currentParameter;
        private string _content;
        private string _documentation;
        private ITrackingSpan _applicableToSpan;
        private ReadOnlyCollection<IParameter> _parameters;
        private string _printContent;

        internal AsmSignature(ITextBuffer subjectBuffer, string content, string doc, ReadOnlyCollection<IParameter> parameters) {
            _subjectBuffer = subjectBuffer;
            _content = content;
            _documentation = doc;
            _parameters = parameters;
            _subjectBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(OnSubjectBufferChanged);
        }
        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public IParameter CurrentParameter {
            get { return _currentParameter; }
            internal set {
                if (_currentParameter != value) {
                    IParameter prevCurrentParameter = _currentParameter;
                    _currentParameter = value;
                    this.RaiseCurrentParameterChanged(prevCurrentParameter, _currentParameter);
                }
            }
        }

        private void RaiseCurrentParameterChanged(IParameter prevCurrentParameter, IParameter newCurrentParameter) {
            EventHandler<CurrentParameterChangedEventArgs> tempHandler = this.CurrentParameterChanged;
            if (tempHandler != null) {
                tempHandler(this, new CurrentParameterChangedEventArgs(prevCurrentParameter, newCurrentParameter));
            }
        }

        internal void ComputeCurrentParameter() {
            if (Parameters.Count == 0) {
                this.CurrentParameter = null;
                return;
            }

            //the number of commas in the string is the index of the current parameter
            string sigText = ApplicableToSpan.GetText(_subjectBuffer.CurrentSnapshot);

            int currentIndex = 0;
            int commaCount = 0;
            while (currentIndex < sigText.Length) {
                int commaIndex = sigText.IndexOf(',', currentIndex);
                if (commaIndex == -1) {
                    break;
                }
                commaCount++;
                currentIndex = commaIndex + 1;
            }

            if (commaCount < Parameters.Count) {
                this.CurrentParameter = Parameters[commaCount];
            } else {
                //too many commas, so use the last parameter as the current one.
                this.CurrentParameter = Parameters[Parameters.Count - 1];
            }
        }

        internal void OnSubjectBufferChanged(object sender, TextContentChangedEventArgs e) {
            this.ComputeCurrentParameter();
        }

        public ITrackingSpan ApplicableToSpan {
            get { return (_applicableToSpan); }
            internal set { _applicableToSpan = value; }
        }

        public string Content {
            get { return (_content); }
            internal set { _content = value; }
        }

        public string Documentation {
            get { return (_documentation); }
            internal set { _documentation = value; }
        }

        public ReadOnlyCollection<IParameter> Parameters {
            get { return (_parameters); }
            internal set { _parameters = value; }
        }

        public string PrettyPrintedContent {
            get { return (_printContent); }
            internal set { _printContent = value; }
        }
    }

    internal class AsmSignatureHelpSource : ISignatureHelpSource {
        private readonly ITextBuffer _textBuffer;
        private readonly ITextStructureNavigator _navigator;

        public AsmSignatureHelpSource(ITextBuffer textBuffer, ITextStructureNavigator nav) {
            _textBuffer = textBuffer;
            _navigator = nav;
        }

        public void AugmentSignatureHelpSession(ISignatureHelpSession session, IList<ISignature> signatures) {
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: AugmentSignatureHelpSession");

            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            int position = session.GetTriggerPoint(_textBuffer).GetPosition(snapshot);
            ITrackingSpan applicableToSpan = _textBuffer.CurrentSnapshot.CreateTrackingSpan(new Span(position, 0), SpanTrackingMode.EdgeInclusive, 0);

            if (false) { //move the point back so it's in the preceding word
                string previousWord = _navigator.GetExtentOfWord(new SnapshotPoint(snapshot, position - 1)).Span.GetText().ToUpper();
                this.fill_OLD(previousWord, signatures, applicableToSpan);
            } else {
                this.fill(snapshot.GetLineFromPosition(position), position, signatures, applicableToSpan);
            }
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

        private AsmSignature CreateSignature(ITextBuffer textBuffer, string methodSig, string methodDoc, ITrackingSpan span) {
            AsmSignature sig = new AsmSignature(textBuffer, methodSig, methodDoc, null);
            textBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(sig.OnSubjectBufferChanged);

            //find the parameters in the method signature (expect OPCODE one, two, three)
            string[] pars = methodSig.Split(new char[] { ',', ' '});
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
                    paramList.Add(new AsmParameter("Documentation for the parameter.", locus, param, sig));
                }
            }

            sig.Parameters = new ReadOnlyCollection<IParameter>(paramList);
            sig.ApplicableToSpan = span;
            sig.ComputeCurrentParameter();
            return sig;
        }

        private void fill(ITextSnapshotLine line, int position, IList<ISignature> signatures, ITrackingSpan applicableToSpan) {

            string lineStr = line.GetText();
            int positionInLine = position - line.Start;
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: lineStr=" + lineStr+ "; positionInLine=" + positionInLine);

            var t = AsmSourceTools.parseLine(lineStr);
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill: Mnemonic=" + t.Item2 + "; args=" + string.Join(",", t.Item3));

            switch (t.Item2) {
                case Mnemonic.ADD:
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, imm8", "Add imm8 to r/m8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm16", "Add imm16 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm32", "Add imm32 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm32", "Add imm32 sign-extended to 64-bits to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm8", "Add sign-extended imm8 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm8", "Add sign-extended imm8 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm8", "Add sign-extended imm8 to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, r8", "Add r8 to r/m8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, r16", "Add r16 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, r32", "Add r32 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, r64", "Add r64 to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r8, r/m8", "Add r/m8 to r8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r16, r/m16", "Add r/m16 to r16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r32, r/m32", "Add r/m32 to r32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r64, r/m64", "Add r/m64 to r64.", applicableToSpan));
                    break;
                case Mnemonic.AND:
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, imm8", "Add imm8 to r/m8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm16", "Add imm16 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm32", "Add imm32 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm32", "Add imm32 sign-extended to 64-bits to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm8", "Add sign-extended imm8 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm8", "Add sign-extended imm8 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm8", "Add sign-extended imm8 to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, r8", "Add r8 to r/m8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, r16", "Add r16 to r/m16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, r32", "Add r32 to r/m32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, r64", "Add r64 to r/m64.", applicableToSpan));

                    signatures.Add(CreateSignature(_textBuffer, "ADD r8, r/m8", "Add r/m8 to r8.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r16, r/m16", "Add r/m16 to r16.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r32, r/m32", "Add r/m32 to r32.", applicableToSpan));
                    signatures.Add(CreateSignature(_textBuffer, "ADD r64, r/m64", "Add r/m64 to r64.", applicableToSpan));
                    break;
            }
        }

        private void fill_OLD(string previousWord, IList<ISignature> signatures, ITrackingSpan applicableToSpan) {
            AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: fill_OLD: previousWord=" + previousWord);

            if (previousWord.Equals("ADD")) {
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, imm8", "Add imm8 to r/m8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm16", "Add imm16 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm32", "Add imm32 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm32", "Add imm32 sign-extended to 64-bits to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, imm8", "Add sign-extended imm8 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, imm8", "Add sign-extended imm8 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, imm8", "Add sign-extended imm8 to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "ADD r/m8, r8", "Add r8 to r/m8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m16, r16", "Add r16 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m32, r32", "Add r32 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r/m64, r64", "Add r64 to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "ADD r8, r/m8", "Add r/m8 to r8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r16, r/m16", "Add r/m16 to r16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r32, r/m32", "Add r/m32 to r32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "ADD r64, r/m64", "Add r/m64 to r64.", applicableToSpan));

            } else if (previousWord.Equals("AND")) {
                signatures.Add(CreateSignature(_textBuffer, "AND r/m8, imm8", "Add imm8 to r/m8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m16, imm16", "Add imm16 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m32, imm32", "Add imm32 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m64, imm32", "Add imm32 sign-extended to 64-bits to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "AND r/m16, imm8", "Add sign-extended imm8 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m32, imm8", "Add sign-extended imm8 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m64, imm8", "Add sign-extended imm8 to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "AND r/m8, r8", "Add r8 to r/m8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m16, r16", "Add r16 to r/m16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m32, r32", "Add r32 to r/m32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r/m64, r64", "Add r64 to r/m64.", applicableToSpan));

                signatures.Add(CreateSignature(_textBuffer, "AND r8, r/m8", "Add r/m8 to r8.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r16, r/m16", "Add r/m16 to r16.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r32, r/m32", "Add r/m32 to r32.", applicableToSpan));
                signatures.Add(CreateSignature(_textBuffer, "AND r64, r/m64", "Add r/m64 to r64.", applicableToSpan));
            }
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