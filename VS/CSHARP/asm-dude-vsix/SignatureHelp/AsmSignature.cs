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
    using System.Collections.ObjectModel;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;

    internal class AsmSignature : ISignature
    {
        private readonly ITextBuffer subjectBuffer_;

        private IParameter currentParameter_;
        private string content_;
        private string documentation_;
        private ITrackingSpan applicableToSpan_;
        private ReadOnlyCollection<IParameter> parameters_;
        private string printContent_;

        internal AsmSignature(ITextBuffer subjectBuffer, string content, string doc, ReadOnlyCollection<IParameter> parameters)
        {
            this.subjectBuffer_ = subjectBuffer ?? throw new ArgumentNullException(nameof(subjectBuffer));
            this.content_ = content;
            this.documentation_ = doc;
            this.parameters_ = parameters;
            this.subjectBuffer_.Changed += new EventHandler<TextContentChangedEventArgs>(this.OnSubjectBufferChanged);
        }

        public event EventHandler<CurrentParameterChangedEventArgs> CurrentParameterChanged;

        public IParameter CurrentParameter
        {
            get { return this.currentParameter_; }

            internal set
            {
                if (this.currentParameter_ != value)
                {
                    IParameter prevCurrentParameter = this.currentParameter_;
                    this.currentParameter_ = value;
                    this.RaiseCurrentParameterChanged(prevCurrentParameter, this.currentParameter_);
                }
            }
        }

        private void RaiseCurrentParameterChanged(IParameter prevCurrentParameter, IParameter newCurrentParameter)
        {
            this.CurrentParameterChanged?.Invoke(this, new CurrentParameterChangedEventArgs(prevCurrentParameter, newCurrentParameter));
        }

        public static int Count_Commas(string str)
        {
            int currentIndex = 0;
            int commaCount = 0;
            while (currentIndex < str.Length)
            {
                int commaIndex = str.IndexOf(',', currentIndex);
                if (commaIndex == -1)
                {
                    break;
                }
                commaCount++;
                currentIndex = commaIndex + 1;
            }
            return commaCount;
        }

        internal void Compute_Current_Parameter()
        {
            //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: computeCurrentParameter");

            int nParameters = this.Parameters.Count;
            if (nParameters == 0)
            {
                this.CurrentParameter = null;
                return;
            }

            //the number of commas in the current line is the index of the current parameter
            SnapshotPoint position = this.applicableToSpan_.GetStartPoint(this.subjectBuffer_.CurrentSnapshot);
            string lineStr = this.subjectBuffer_.CurrentSnapshot.GetLineFromPosition(position).GetText();
            //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: computeCurrentParameter. lineStr=" + lineStr);

            int commaCount = Count_Commas(lineStr);
            //AsmDudeToolsStatic.Output_INFO("AsmSignatureHelpSource: computeCurrentParameter. commaCount="+ commaCount);

            if (commaCount < nParameters)
            {
                this.CurrentParameter = this.Parameters[commaCount];
            }
            else
            {
                this.CurrentParameter = null;
            }
        }

        internal void OnSubjectBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            this.Compute_Current_Parameter();
        }

        public ITrackingSpan ApplicableToSpan
        {
            get { return this.applicableToSpan_; }
            internal set { this.applicableToSpan_ = value; }
        }

        public string Content
        {
            get { return this.content_; }
            internal set { this.content_ = value; }
        }

        public string Documentation
        {
            get { return this.documentation_; }
            internal set { this.documentation_ = value; }
        }

        public ReadOnlyCollection<IParameter> Parameters
        {
            get { return this.parameters_; }
            internal set { this.parameters_ = value; }
        }

        public string PrettyPrintedContent
        {
            get { return this.printContent_; }
            internal set { this.printContent_ = value; }
        }
    }
}
