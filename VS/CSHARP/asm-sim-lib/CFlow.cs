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

using AsmTools;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AsmSim
{
    public class CFlow
    {
        public static readonly char LINENUMBER_SEPARATOR = '!';

        private string _programStr = "";
        private IList<(string label, Mnemonic mnemonic, string[] args)> _sourceCode;
        private readonly IDictionary<int, IList<(int lineNumber, bool isBranch)>> _incomingLines;

        //private readonly BidirectionalGraph<int, IEdge<int>> _data; //TODO use QuickGraph

        /// <summary>Constructor that creates an empty CFlow</summary>
        public CFlow() : this("") {}

        public CFlow(string programStr)
        {
            this._incomingLines = new Dictionary<int, IList<(int lineNumber, bool isBranch)>>();
            this.Update(programStr);
        }

        /// <summary>Update this CFlow with the provided programStr: return true if this CFlow has changed.</summary>
        public bool Update(string programStr)
        {
            //TODO make intelligent code: return true if something has changed.
            //if (this._programStr.Equals(programStr,StringComparison.OrdinalIgnoreCase))
            //{
            //    Console.WriteLine("INFO: CFlow:Update: superfluous update. Doing nothing");
            //    return false;
            //}
            this._programStr = programStr;
            this._incomingLines.Clear();
            this._sourceCode = this.GetLines(programStr);
            return true;
        }

        public int NLines { get { return this._sourceCode.Count; } }

        public int LastLineNumber {  get { return this._sourceCode.Count - 1; } }

        public bool HasLine(int lineNumber)
        {
            return (lineNumber >= 0) && (lineNumber < this._sourceCode.Count);
        }

        public (string Label, Mnemonic Mnemonic, string[] Args) GetLine(int lineNumber)
        {
            Debug.Assert(lineNumber >= 0);
            if (lineNumber >= this._sourceCode.Count)
            {
                Console.WriteLine("ERROR: CFlow:geLine: lineNumber " + lineNumber + " does not exist");
                return ("", Mnemonic.NONE, null);
            }
            return this._sourceCode[lineNumber];
        }

        public string GetLineStr(int lineNumber)
        {
            return (this.HasLine(lineNumber)) ? CFlow.ToString(this.GetLine(lineNumber)) : "";
        }
        /*
        /// <summary>for the provided mergePoint, return the branchKeys </summary>
        public (string Regular, string Branch) getBranchKeys(int lineNumber)
        {
            if (this.IsMergePoint(lineNumber))
            {
                IList<(int LineNumber, bool IsBranch)> prevLines = new List<(int, bool)>(this.GetPrevLineNumber(lineNumber));
                int lineNumber1 = prevLines[0].LineNumber;
                int lineNumber2 = prevLines[1].LineNumber;

                int mergedLineNumber = this.GetFirstMergePoint(lineNumber1, lineNumber2);
                this.GetLine(mergedLineNumber);


            }
            else throw new Exception();
        }

        /// <summary> Get the first lineNumber in which the branches in the both lineNumbers merge</summary>
        public int GetFirstMergePoint(int lineNumber1, int lineNumber2)
        {
            if ()
        }
        */

        /// <summary>
        /// Get the previous line numbers and whether those line numbers branched to the current line number
        /// </summary>
        public IEnumerable<(int LineNumber, bool IsBranch)> GetPrevLineNumber(int lineNumber)
        {
            if (this._incomingLines.TryGetValue(lineNumber, out IList<(int, bool)> incoming))
            {
                return incoming;
            }
            return Enumerable.Empty<(int LineNumber, bool IsBranch)>();
        }

        public (int Regular, int Branch) GetNextLineNumber(int lineNumber)
        {
            int lineNumberRegular = -1;
            int lineNumberBranch = -1;

            //TODO make faster!!
            foreach (var x in this._incomingLines)
            {
                foreach (var y in x.Value)
                {
                    if (y.lineNumber == lineNumber)
                    {
                        if (y.isBranch)
                        {
                            lineNumberBranch = x.Key;
                        }
                        else
                        {
                            lineNumberRegular = x.Key;
                        }
                        if ((lineNumberBranch != -1) && (lineNumberRegular != -1))
                        {
                            return (Regular: lineNumberRegular, Branch: lineNumberBranch);
                        }
                    }
                }
            }
            return (Regular: lineNumberRegular, Branch: lineNumberBranch);
        }

        /// <summary>A LoopBranchPoint is a BranchPoint that choices between leaving the loop or staying in the loop.
        /// BranchToExitLoop is true if the branch code flow is used to leave the loop.</summary>
        public (bool IsLoopBranchPoint, bool BranchToExitLoop) IsLoopBranchPoint(int lineNumber)
        {
            if (this.IsBranchPoint(lineNumber))
            {
                var next = this.GetNextLineNumber(lineNumber);
                bool hasCodePath_Branch = HasCodePath(next.Branch, lineNumber);
                bool hasCodePath_Regular = HasCodePath(next.Regular, lineNumber);

                if (hasCodePath_Branch && !hasCodePath_Regular)
                {
                    return (IsLoopBranchPoint: true, BranchToExitLoop: false);
                }
                else if (!hasCodePath_Branch && hasCodePath_Regular)
                {
                    return (IsLoopBranchPoint: true, BranchToExitLoop: true);
                }
            }
            return (false, false);
        }

        /// <summary>A LoopMergePoint is a MergePoint that merges a loop with its begin point.</summary>
        public (bool IsLoopMergePoint, int LoopLineNumber) IsLoopMergePoint(int lineNumber)
        {
            if (this.IsMergePoint(lineNumber))
            {
                int numberOfLoops = 0;
                int loopLineNumber = -1;
                // TODO return the smalles loop 

                foreach (var v in this.GetPrevLineNumber(lineNumber))
                {
                    if (HasCodePath(lineNumber, v.LineNumber))
                    {
                        numberOfLoops++;
                        loopLineNumber = v.LineNumber; 
                    }
                }
                if (numberOfLoops > 0)
                {
                    return (true, loopLineNumber);
                }
            }
            return (false, 0);
        }

        public bool HasCodePath(int lineNumber_from, int lineNumber_to)
        {
            //TODO this can be made faster without building the full set of future lineNumbers
            return this.FutureLineNumbers(lineNumber_from).Contains(lineNumber_to);
        }

        public ISet<int> FutureLineNumbers(int lineNumber)
        {
            ISet<int> result = new HashSet<int>();
            FutureLineNumbers_Local(lineNumber);
            return result;

            void FutureLineNumbers_Local(int lineNumber2)
            {
                if (lineNumber2 == -1) return;
                if (this.HasLine(lineNumber2) && !result.Contains(lineNumber2))
                {
                    result.Add(lineNumber2);
                    var next = this.GetNextLineNumber(lineNumber2);
                    FutureLineNumbers_Local(next.Regular);
                    FutureLineNumbers_Local(next.Branch);
                }
            }
        }

        /// <summary>A BranchPoint is an code line that has two next states (that need not be different)</summary>
        public bool IsBranchPoint(int lineNumber)
        {
            if (this.HasLine(lineNumber))
            {
                var lineContent = this.GetLine(lineNumber);
                StaticJump(lineContent.Mnemonic, lineContent.Args, lineNumber, out int jumpTo1, out int jumpTo2);
                return ((jumpTo1 >= 0) && (jumpTo2 >= 0));
            }
            else
            {
                return false;
            }
        }

        /// <summary>A MergePoint is a code line which has at least two control flows that merge into this line</summary>
        public bool IsMergePoint(int lineNumber)
        {
            var enumerator = this.GetPrevLineNumber(lineNumber).GetEnumerator();
            return (enumerator.MoveNext() && enumerator.MoveNext());
        }

        #region ToString Methods

        public static string ToString((string label, Mnemonic mnemonic, string[] args) t)
        {
            string arguments = "";

            switch (t.args.Length)
            {
                case 0: break;
                case 1: arguments = t.args[0]; break;
                case 2: arguments = t.args[0] + ", " + t.args[1]; break;
                case 3: arguments = t.args[0] + ", " + t.args[1] + ", " + t.args[2]; break;
                default: break;
            }
            if ((t.mnemonic == Mnemonic.UNKNOWN) && (t.label.Length > 0))
            {
                // line with only a label and no opcode
                return string.Format("{0}:", t.label);
            }
            else
            {
                return string.Format("{0}{1} {2}", ((t.label.Length > 0) ? (t.label + ": ") : ""), t.mnemonic, arguments);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < this._sourceCode.Count; ++i)
            {
                sb.Append("Line " + i + ": ");
                sb.Append(this.GetLineStr(i));
                sb.Append(" [Prev:");
                foreach (var previous in this.GetPrevLineNumber(i))
                {
                    sb.Append(previous.LineNumber + ((previous.IsBranch) ? "B" : "R") + ",");  // B=Branching; R=Regular Continuation
                }
                sb.Append("][Next:");
                var nextLineNumber = this.GetNextLineNumber(i);
                if (nextLineNumber.Regular != -1) sb.Append(nextLineNumber.Regular + "R,");
                if (nextLineNumber.Branch != -1) sb.Append(nextLineNumber.Branch + "B");
                sb.AppendLine("]");
            }
            return sb.ToString();
        }

        #endregion ToString Methods

        #region Private Methods

        private IList<(string label, Mnemonic mnemonic, string[] args)> GetLines(string programStr)
        {
            #region parse to find all labels
            IDictionary<string, int> labels = this.GetLabels(programStr);
            // replace all labels by annotated label
            foreach (KeyValuePair<string, int> entry in labels)
            {
                if (entry.Key.Contains(LINENUMBER_SEPARATOR.ToString()))
                {
                    Console.WriteLine("WARNING: CFLOW:GetLines: label " + entry.Key + " has an "+ LINENUMBER_SEPARATOR);
                }
                string newLabel = entry.Key + LINENUMBER_SEPARATOR + entry.Value;
                //Console.WriteLine("INFO: ControlFlow:getLines: Replacing label " + entry.Key + " with " + newLabel);
                programStr = programStr.Replace(entry.Key, newLabel);
            }
            #endregion
            #region Populate IncomingLines
            string[] lines = programStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            IList<(string, Mnemonic, string[])> data = new List<(string, Mnemonic, string[])>(lines.Length);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                var line = AsmSourceTools.ParseLine(lines[lineNumber]);
                data.Add((line.Label, line.Mnemonic, line.Args));

                this.StaticJump(line.Mnemonic, line.Args, lineNumber, out int jumpTo1, out int jumpTo2);
                if (jumpTo1 != -1)
                {
                    this.AddToIncomingLines(lineNumber, jumpTo1, false);
                }
                if (jumpTo2 != -1)
                {
                    this.AddToIncomingLines(lineNumber, jumpTo2, true);
                }
            }
            #endregion
            return data;
        }

        private void AddToIncomingLines(int jumpFrom, int jumpTo, bool isBranch)
        {
            if (this._incomingLines.ContainsKey(jumpTo))
            {
                this._incomingLines[jumpTo].Add((jumpFrom, isBranch));
            }
            else
            {
                this._incomingLines.Add(jumpTo, new List<(int, bool)> { (jumpFrom, isBranch) });
            }
        }

        private void StaticJump(Mnemonic mnemonic, string[] args, int lineNumber, out int jumpTo1, out int jumpTo2)
        {
            jumpTo1 = -1;
            jumpTo2 = -1;

            switch (mnemonic)
            {
                case Mnemonic.UNKNOWN:
                    jumpTo1 = lineNumber + 1;
                    break;
                case Mnemonic.JMP:
                    if (args.Length > 0)
                    {
                        jumpTo2 = ToolsZ3.GetLineNumberFromLabel(args[0], LINENUMBER_SEPARATOR);
                    }
                    break;
                case Mnemonic.JE:
                case Mnemonic.JZ:
                case Mnemonic.JNE:
                case Mnemonic.JNZ:
                case Mnemonic.JA:
                case Mnemonic.JNBE:
                case Mnemonic.JAE:
                case Mnemonic.JNB:
                case Mnemonic.JB:
                case Mnemonic.JNAE:
                case Mnemonic.JBE:
                case Mnemonic.JNA:
                case Mnemonic.JG:
                case Mnemonic.JNLE:
                case Mnemonic.JGE:
                case Mnemonic.JNL:
                case Mnemonic.JL:
                case Mnemonic.JNGE:
                case Mnemonic.JLE:
                case Mnemonic.JNG:
                case Mnemonic.JC:
                case Mnemonic.JNC:
                case Mnemonic.JO:
                case Mnemonic.JNO:
                case Mnemonic.JS:
                case Mnemonic.JNS:
                case Mnemonic.JPO:
                case Mnemonic.JNP:
                case Mnemonic.JPE:
                case Mnemonic.JP:
                case Mnemonic.JCXZ:
                case Mnemonic.JECXZ:
                case Mnemonic.JRCXZ:
                case Mnemonic.LOOP:
                case Mnemonic.LOOPZ:
                case Mnemonic.LOOPE:
                case Mnemonic.LOOPNZ:
                case Mnemonic.LOOPNE:
                case Mnemonic.CALL:
                    jumpTo1 = lineNumber + 1;
                    if (args.Length > 0)
                    {
                        jumpTo2 = ToolsZ3.GetLineNumberFromLabel(args[0], LINENUMBER_SEPARATOR);
                    }
                    break;
                case Mnemonic.UD2: return;
                case Mnemonic.RET:
                case Mnemonic.IRET:
                case Mnemonic.INT:
                case Mnemonic.INTO:
                   //throw new NotImplementedException();
                default:
                    jumpTo1 = lineNumber + 1;
                    break;

            }
            //Console.WriteLine("INFO: StaticControlFlow: "+StaticControlFlow.ToString(tup)+"; jumpTo1=" + jumpTo1 + "; jumpTo2=" + jumpTo2);
            return;
        }

        /// <summary>Get all labels with the line number on which it is defined</summary>
        private IDictionary<string, int> GetLabels(string text)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();
            string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string line = lines[lineNumber];
                (bool, int, int) labelPos = AsmTools.AsmSourceTools.GetLabelDefPos(line);
                if (labelPos.Item1)
                {
                    int labelBeginPos = labelPos.Item2;
                    int labelEndPos = labelPos.Item3;
                    string label = line.Substring(labelBeginPos, labelEndPos - labelBeginPos);
                    if (result.ContainsKey(label))
                    {
                        Console.WriteLine(string.Format("WARNING: getLabels: found a clashing label \"{0}\" at line=\"{1}\".", label, lineNumber));
                    }
                    else
                    {
                        result.Add(label, lineNumber);
                    }
                    //Console.WriteLine(string.Format("INFO: getLabels: label=\"{0}\"; line=\"{1}\".", label, lineNumber));
                }
            }
            return result;
        }
        #endregion Private Methods
    }
}
