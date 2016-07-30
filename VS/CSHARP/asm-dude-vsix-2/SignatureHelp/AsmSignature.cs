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

using AsmDude.Tools;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.ObjectModel;

namespace AsmDude.SignatureHelp {

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
            _subjectBuffer.Changed += new EventHandler<TextContentChangedEventArgs>(this.OnSubjectBufferChanged);
        }
        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public IParameter CurrentParameter {
            get { return this._currentParameter; }
            internal set {
                if (this._currentParameter != value) {
                    IParameter prevCurrentParameter = this._currentParameter;
                    this._currentParameter = value;
                    this.RaiseCurrentParameterChanged(prevCurrentParameter, this._currentParameter);
                }
            }
        }

        private void RaiseCurrentParameterChanged(IParameter prevCurrentParameter, IParameter newCurrentParameter) {
            EventHandler<CurrentParameterChangedEventArgs> tempHandler = this.CurrentParameterChanged;
            if (tempHandler != null) {
                tempHandler(this, new CurrentParameterChangedEventArgs(prevCurrentParameter, newCurrentParameter));
            }
        }

        public static int countCommas(string str) {
            int currentIndex = 0;
            int commaCount = 0;
            while (currentIndex < str.Length) {
                int commaIndex = str.IndexOf(',', currentIndex);
                if (commaIndex == -1) {
                    break;
                }
                commaCount++;
                currentIndex = commaIndex + 1;
            }
            return commaCount;
        }

        internal void computeCurrentParameter() {
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: computeCurrentParameter");

            int nParameters = this.Parameters.Count;
            if (nParameters == 0) {
                this.CurrentParameter = null;
                return;
            }

            //the number of commas in the current line is the index of the current parameter
            SnapshotPoint position = _applicableToSpan.GetStartPoint(_subjectBuffer.CurrentSnapshot);
            string lineStr = _subjectBuffer.CurrentSnapshot.GetLineFromPosition(position).GetText();
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: computeCurrentParameter. lineStr=" + lineStr);

            int commaCount = AsmSignature.countCommas(lineStr);
            //AsmDudeToolsStatic.Output("INFO: AsmSignatureHelpSource: computeCurrentParameter. commaCount="+ commaCount);

            if (commaCount < nParameters) {
                this.CurrentParameter = this.Parameters[commaCount];
            } else {
                this.CurrentParameter = null;
            }
        }

        internal void OnSubjectBufferChanged(object sender, TextContentChangedEventArgs e) {
            this.computeCurrentParameter();
        }

        public ITrackingSpan ApplicableToSpan {
            get { return (this._applicableToSpan); }
            internal set { this._applicableToSpan = value; }
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
}
