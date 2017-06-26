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
using System.Diagnostics;
using System.Text;
using System.Linq;

using AsmTools;
using QuickGraph;

namespace AsmSim
{
    public class StaticFlow
    {
        public static readonly char LINENUMBER_SEPARATOR = '!';
        public static readonly int MAX_LINES = 200;

        private readonly Tools _tools;

        private readonly IList<(string Label, Mnemonic Mnemonic, string[] Args)> _parsed_Code_A;
        private readonly IList<(string Label, Mnemonic Mnemonic, string[] Args)> _parsed_Code_B;
        private bool _use_Parsed_Code_A;

        private readonly BidirectionalGraph<int, TaggedEdge<int, bool>> _graph;

        /// <summary>Constructor that creates an empty CFlow</summary>
        public StaticFlow(Tools tools)
        {
            //Console.WriteLine("INFO: CFlow: constructor");
            this._tools = tools;
            this._use_Parsed_Code_A = true;
            this._parsed_Code_A = new List<(string Label, Mnemonic Mnemonic, string[] Args)>();
            this._parsed_Code_B = new List<(string Label, Mnemonic Mnemonic, string[] Args)>();
            this._graph = new BidirectionalGraph<int, TaggedEdge<int, bool>>(true); // allowParallesEdges is true because of conditional jumps to the next line
        }

        #region Getters

        public StateConfig Create_StateConfig()
        {
            return this.Create_StateConfig(0, this.LastLineNumber-1);
        }

        public StateConfig Create_StateConfig(
            int lineNumberBegin,
            int lineNumberEnd)
        {
            ISet<Rn> regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            bool mem = false;
            var dummyKeys = ("", "", "");
            for (int lineNumber = lineNumberBegin; lineNumber <= lineNumberEnd; lineNumber++)
            {
                var content = this.Get_Line(lineNumber);
                using (var opcodeBase = Runner.InstantiateOpcode(content.Mnemonic, content.Args, dummyKeys, this._tools))
                {
                    if (opcodeBase != null)
                    {
                        flags |= (opcodeBase.FlagsReadStatic | opcodeBase.FlagsWriteStatic);
                        foreach (Rn r in opcodeBase.RegsReadStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                        foreach (Rn r in opcodeBase.RegsWriteStatic) regs.Add(RegisterTools.Get64BitsRegister(r));
                        mem |= opcodeBase.MemReadWriteStatic;
                    }
                }
            }

            StateConfig config = new StateConfig();
            config.Set_All_Off();
            config.Set_Flags_On(flags);
            foreach (Rn reg in regs) config.Set_Reg_On(reg);
            config.mem = mem;
            return config;
        }

        public string Get_Key(int lineNumber)
        {
            if (false)
            {
                return Tools.CreateKey(this._tools.Rand);
            }
            else
            {
                return "!" + (lineNumber).ToString();
            }
        }
        public (string Key1, string Key2) Get_Key((int lineNumber1, int lineNumber2) lineNumber)
        {
            if (true)
            {
                string key1 = Get_Key(lineNumber.lineNumber1);
                string key2 = Get_Key(lineNumber.lineNumber2);
                return (Key1: key1, Key2: key2);
            } else
            {
                string key1 = Tools.CreateKey(this._tools.Rand);
                string key2 = key1 + "B";
                return (Key1: key1, Key2: key2);
            }
        }

        public int NLines { get { return this.Current.Count; } }

        public int FirstLineNumber {
            get {
                int lineNumber = 0;
                var current = this.Current;
                while (current[lineNumber].Mnemonic == Mnemonic.NONE)
                {
                    lineNumber++;
                }
                return lineNumber;
            }
        }
        public int LastLineNumber { get { return this.Current.Count; } }

        public bool HasLine(int lineNumber)
        {
            return (lineNumber >= 0) && (lineNumber < this.Current.Count);
        }

        public (Mnemonic Mnemonic, string[] Args) Get_Line(int lineNumber)
        {
            Debug.Assert(lineNumber >= 0);
            if (lineNumber >= this.Current.Count)
            {
                Console.WriteLine("WARING: CFlow:geLine: lineNumber " + lineNumber + " does not exist");
                return (Mnemonic.NONE, null);
            }
            var v = this.Current[lineNumber];

            return (v.Mnemonic, v.Args);
        }

        public string Get_Line_Str(int lineNumber)
        {
            return (this.HasLine(lineNumber)) ? StaticFlow.ToString(this.Current[lineNumber]) : "";
        }
        
        public bool Has_Prev_LineNumber(int lineNumber)
        {
            return !this._graph.IsInEdgesEmpty(lineNumber);
        }

        public bool Has_Next_LineNumber(int lineNumber)
        {
            return !this._graph.IsOutEdgesEmpty(lineNumber);
        }

        public int Number_Prev_LineNumbers(int lineNumber)
        {
            return this._graph.InDegree(lineNumber);
        }

        public int Number_Next_LineNumbers(int lineNumber)
        {
            return this._graph.OutDegree(lineNumber);
        }

        /// <summary>
        /// Get the previous line numbers and whether those line numbers branched to the current line number
        /// </summary>
        public IEnumerable<(int LineNumber, bool IsBranch)> Get_Prev_LineNumber(int lineNumber)
        {
            foreach (var v in this._graph.InEdges(lineNumber)) yield return (v.Source, v.Tag);
        }

        public (int Regular, int Branch) Get_Next_LineNumber(int lineNumber)
        {
            int Regular = -1;
            int Branch = -1;

            foreach (var v in this._graph.OutEdges(lineNumber))
            {
                if (v.Tag)
                {
                    Branch = v.Target;
                }
                else
                {
                    Regular = v.Target;
                }
            }
            return (Regular: Regular, Branch: Branch);
        }

        /// <summary>A LoopBranchPoint is a BranchPoint that choices between leaving the loop or staying in the loop.
        /// BranchToExitLoop is true if the branch code flow is used to leave the loop.</summary>
        public (bool IsLoopBranchPoint, bool BranchToExitLoop) Is_Loop_Branch_Point(int lineNumber)
        {
            if (this.Is_Branch_Point(lineNumber))
            {
                var next = this.Get_Next_LineNumber(lineNumber);
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
        public (bool IsLoopMergePoint, int LoopLineNumber) Is_Loop_Merge_Point(int lineNumber)
        {
            if (this.Is_Merge_Point(lineNumber))
            {
                int numberOfLoops = 0;
                int loopLineNumber = -1;
                // TODO return the smallest loop 

                foreach (var v in this.Get_Prev_LineNumber(lineNumber))
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
                    var next = this.Get_Next_LineNumber(lineNumber2);
                    FutureLineNumbers_Local(next.Regular);
                    FutureLineNumbers_Local(next.Branch);
                }
            }
        }

        /// <summary>A BranchPoint is an code line that has two next states (that need not be different)</summary>
        public bool Is_Branch_Point(int lineNumber)
        {
            return (this._graph.OutDegree(lineNumber) > 1);
        }

        /// <summary>A MergePoint is a code line which has at least two control flows that merge into this line</summary>
        public bool Is_Merge_Point(int lineNumber)
        {
            return (this._graph.InDegree(lineNumber) > 1);
        }

        #endregion

        #region Setters
        /// <summary>Update this CFlow with the provided programStr: return true if this CFlow has changed.</summary>
        public bool Update(string programStr, bool removeEmptyLines = true)
        {
            //Console.WriteLine("INFO: CFlow:Update_Lines");
            this._use_Parsed_Code_A = !this._use_Parsed_Code_A;

            #region Restrict input to max number of lines
            {
                string[] lines = programStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                if (lines.Length > MAX_LINES)
                {
                    Array.Resize(ref lines, MAX_LINES);
                }
                StringBuilder sb = new StringBuilder();
                foreach (string v in lines) sb.AppendLine(v);
                programStr = sb.ToString();
            }
            #endregion
            #region Parse to find all labels
            IDictionary<string, int> labels = this.GetLabels(programStr);
            // replace all labels by annotated label
            foreach (KeyValuePair<string, int> entry in labels)
            {
                if (entry.Key.Contains(LINENUMBER_SEPARATOR.ToString()))
                {
                    Console.WriteLine("WARNING: CFLOW:GetLines: label " + entry.Key + " has an " + LINENUMBER_SEPARATOR);
                }
                string newLabel = entry.Key + LINENUMBER_SEPARATOR + entry.Value;
                //Console.WriteLine("INFO: ControlFlow:getLines: Replacing label " + entry.Key + " with " + newLabel);
                programStr = programStr.Replace(entry.Key, newLabel);
            }
            #endregion

            var previous = this.Previous;
            var current = this.Current;

            current.Clear();
            this._graph.Clear();

            #region Populate IncomingLines
            {
                string[] lines = programStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

                for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
                {
                    var line = AsmSourceTools.ParseLine(lines[lineNumber]);
                    this.EvalArgs(ref line.Args);
                    current.Add((line.Label, line.Mnemonic, line.Args));

                    (int jumpTo1, int jumpTo2) = this.Static_Jump(line.Mnemonic, line.Args, lineNumber);
                    if (jumpTo1 != -1)
                    {
                        this.Add_Edge(lineNumber, jumpTo1, false);
                    }
                    if (jumpTo2 != -1)
                    {
                        this.Add_Edge(lineNumber, jumpTo2, true);
                    }
                }
            }
            #endregion

            if (removeEmptyLines)
            {
                this.Compress();
            }

            #region Test if different from previous version
            bool equal = true;

            if (current.Count != previous.Count) {
                equal = false;
            } else
            {
                for (int lineNumber = 0; lineNumber < current.Count; ++lineNumber)
                {
                    var previous_line = previous[lineNumber];
                    var current_line = current[lineNumber];
                    if (previous_line.Label != current_line.Label)
                    {
                        equal = false;
                        break;
                    } else if (previous_line.Mnemonic != current_line.Mnemonic)
                    {
                        equal = false;
                        break;
                    } else if (!Enumerable.SequenceEqual(previous_line.Args, current_line.Args)) {
                        equal = false;
                        break;
                    }
                }
            }
            return !equal;
            #endregion
        }
        
        private void EvalArgs(ref string[] args)
        {
            for (int i=0; i<args.Length; ++i)
            {
                var v = ExpressionEvaluator.Evaluate(args[i]);
                if (v.Valid)
                {
                    args[i] = v.Value.ToString();
                }
            }
        }

        /// <summary>Compress this static flow by removing empty lines</summary>
        private void Compress()
        {
            var current = this.Current;
            for (int lineNumber = 0; lineNumber < current.Count; ++lineNumber)
            {
                var c = current[lineNumber];

                if (c.Mnemonic == Mnemonic.NONE) // found an empty line
                {
                    int outDegree = this._graph.OutDegree(lineNumber);
                    if (outDegree == 0)
                    {
                        this._graph.RemoveVertex(lineNumber);
                    }
                    else if (outDegree == 1)
                    {
                        var outEdge = this._graph.OutEdge(lineNumber, 0);
                        this._graph.RemoveEdge(outEdge);
                        int next = outEdge.Target;

                        //Remove this empty line
                        var inEdges = new List<TaggedEdge<int, bool>>(this._graph.InEdges(lineNumber));
                        foreach (var e in inEdges)
                        {
                            this._graph.AddEdge(new TaggedEdge<int, bool>(e.Source, next, e.Tag));
                            this._graph.RemoveEdge(e);
                        }
                    }
                    else
                    {
                        // error: it is not possible for an empty line to be an branching point
                    }
                }
            }
        }
        
        #endregion

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
            if ((t.mnemonic == Mnemonic.NONE) && (t.label.Length > 0))
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

            for (int i = 0; i < this.Current.Count; ++i)
            {
                sb.Append("Line " + i + ": ");
                sb.Append(this.Get_Line_Str(i));
                sb.Append(" [Prev:");
                foreach (var previous in this.Get_Prev_LineNumber(i))
                {
                    sb.Append(previous.LineNumber + ((previous.IsBranch) ? "B" : "R") + ",");  // B=Branching; R=Regular Continuation
                }
                sb.Append("][Next:");
                var nextLineNumber = this.Get_Next_LineNumber(i);
                if (nextLineNumber.Regular != -1) sb.Append(nextLineNumber.Regular + "R,");
                if (nextLineNumber.Branch != -1) sb.Append(nextLineNumber.Branch + "B");
                sb.AppendLine("]");
            }
            return sb.ToString();
        }

        #endregion ToString Methods

        #region Private Methods

        private IList<(string Label, Mnemonic Mnemonic, string[] Args)> Current {
            get { return (this._use_Parsed_Code_A) ? this._parsed_Code_A : this._parsed_Code_B; }
        }
        private IList<(string Label, Mnemonic Mnemonic, string[] Args)> Previous
        {
            get { return (this._use_Parsed_Code_A) ? this._parsed_Code_B : this._parsed_Code_A; }
        }

        private void Add_Edge(int jumpFrom, int jumpTo, bool isBranch)
        {
            //Console.WriteLine("INFO: from " + jumpFrom + " to " + jumpTo + " (branch " + isBranch + ")");
            if (!this._graph.ContainsVertex(jumpFrom)) this._graph.AddVertex(jumpFrom);
            if (!this._graph.ContainsVertex(jumpTo)) this._graph.AddVertex(jumpTo);

            //if (this._graph.ContainsEdge(jumpFrom, jumpTo)) {
            //    Console.WriteLine("INFO: Edge already exists: from " + jumpFrom + " to " + jumpTo + " (branch " + isBranch + ")");
            //}

            this._graph.AddEdge(new TaggedEdge<int, bool>(jumpFrom, jumpTo, isBranch));
        }

        private (int RegularLineNumber, int BranchLineNumber) Static_Jump(Mnemonic mnemonic, string[] args, int lineNumber)
        {
            int jumpTo1 = -1;
            int jumpTo2 = -1;

            switch (mnemonic)
            {
                case Mnemonic.NONE:
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
                case Mnemonic.UD2: break;
                case Mnemonic.RET:
                case Mnemonic.IRET:
                case Mnemonic.INT:
                case Mnemonic.INTO: break;
                   //throw new NotImplementedException();
                default:
                    jumpTo1 = lineNumber + 1;
                    break;

            }
            //Console.WriteLine("INFO: StaticControlFlow: "+StaticControlFlow.ToString(tup)+"; jumpTo1=" + jumpTo1 + "; jumpTo2=" + jumpTo2);
            return (jumpTo1, jumpTo2);
        }

        /// <summary>Get all labels with the line number on which it is defined</summary>
        private IDictionary<string, int> GetLabels(string text)
        {
            IDictionary<string, int> result = new Dictionary<string, int>();
            string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string line = lines[lineNumber];
                var labelPos = AsmTools.AsmSourceTools.GetLabelDefPos(line);
                if (labelPos.Valid)
                {
                    int labelBeginPos = labelPos.BeginPos;
                    int labelEndPos = labelPos.EndPos;
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
