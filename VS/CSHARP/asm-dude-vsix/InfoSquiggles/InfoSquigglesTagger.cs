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

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using AsmTools;
using System.Windows.Media;
using AsmSimZ3;
using AsmDude.SyntaxHighlighting;
using AsmDude.Tools;

namespace AsmDude.InfoSquiggles
{
    internal sealed class InfoSquigglesTagger : ITagger<ErrorTag>
    {
        #region Private Fields

        private readonly ITextBuffer _sourceBuffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;
        private readonly ErrorListProvider _errorListProvider;
        private readonly AsmSimulator _asmSimulator;
        private readonly Brush _foreground;

        private object _updateLock = new object();

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        #endregion Private Fields

        internal InfoSquigglesTagger(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> aggregator,
                AsmSimulator asmSimulator)
        {
            //AsmDudeToolsStatic.Output(string.Format("INFO: LabelErrorTagger: constructor"));
            this._sourceBuffer = buffer;
            this._aggregator = aggregator;
            this._errorListProvider = AsmDudeTools.Instance.Error_List_Provider;
            this._asmSimulator = asmSimulator;
            this._foreground = AsmDudeToolsStatic.GetFontColor();

            this._asmSimulator.Simulate_Done_Event += this.Handle_Simulate_Done_Event;
        }

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {  // there is no content in the buffer
                yield break;
            }

            DateTime time1 = DateTime.Now;

            if (this._asmSimulator.Is_Enabled)
            {
                foreach (IMappingTagSpan<AsmTokenTag> asmTokenTag in this._aggregator.GetTags(spans))
                {
                    SnapshotSpan tagSpan = asmTokenTag.Span.GetSpans(this._sourceBuffer)[0];

                    switch (asmTokenTag.Tag.Type)
                    {
                        case AsmTokenType.Register:
                            {
                                int lineNumber = Get_Linenumber(tagSpan);
                                string regNameStr = tagSpan.GetText();
                                Rn regName = RegisterTools.ParseRn(regNameStr);

                                //AsmSimToolsStatic.Output_INFO(string.Format("AsmSimSquigglesTagger:GetTags: found register " + regName + " at line " + lineNumber));

                                IState_R state = this._asmSimulator.GetState(lineNumber, false);
                                if (state != null)
                                {
                                    // only show squiggles to indicate that information is available
                                    yield return new TagSpan<ErrorTag>(tagSpan, new ErrorTag("infomation"));
                                } else
                                {
                                    //AsmDudeToolsStatic.Output_INFO("InfoSquigglesTagger:GetTags: found register " + regName + " at line " + lineNumber +" but state was null");
                                }
                                break;
                            }
                    }
                }
            }
            AsmDudeToolsStatic.Print_Speed_Warning(time1, "InfoSquigglesTagger");
        }


        #region Private Methods

        private static int Get_Linenumber(SnapshotSpan span)
        {
            return span.Snapshot.GetLineNumberFromPosition(span.Start);
        }

        private int Get_Keyword_Begin_End(int lineNumber, string keyword)
        {
            int lengthKeyword = keyword.Length;
            string lineContent = this._sourceBuffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber).GetText();
            //AsmDudeToolsStatic.Output_INFO("LabelErrorTagger:Get_Keyword_Begin_End lineContent=" + lineContent);

            int startPos = -1;
            for (int i = 0; i < lineContent.Length - lengthKeyword; ++i)
            {
                if (lineContent.Substring(i, lengthKeyword).Equals(keyword))
                {
                    startPos = i;
                    break;
                }
            }

            if (startPos == -1)
            {
                return 0;
            }
            return (startPos | ((startPos + lengthKeyword) << 16));
        }

        private void Handle_Simulate_Done_Event(object sender, CustomEventArgs e)
        {
            //AsmDudeToolsStatic.Output_INFO("AsmSimSquiggleTagger: received an event "+ e.Message);
            this.Update_Squiggles_Tasks_Async();
        }

        async private void Update_Squiggles_Tasks_Async()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                lock (this._updateLock)
                {
                    try
                    {
                        #region Update Tags
                        foreach (ITextSnapshotLine line in this._sourceBuffer.CurrentSnapshot.Lines)
                        {
                            this.TagsChanged(this, new SnapshotSpanEventArgs(line.Extent));
                        }
                        #endregion Update Tags
                    }
                    catch (Exception e)
                    {
                        AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Update_Squiggles_Tasks_Async; e={1}", ToString(), e.ToString()));
                    }
                }
            });

        }
        #endregion Private Methods
    }
}
