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

namespace AsmSim
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using AsmTools;
    using QuikGraph;

    public class StaticFlow
    {
        public static readonly char LINENUMBER_SEPARATOR = '!';
        private static readonly CultureInfo Culture = CultureInfo.CurrentUICulture;

        private readonly Tools tools_;

        private readonly IList<(string label, Mnemonic mnemonic, string[] args)> parsed_Code_A_;
        private readonly IList<(string label, Mnemonic mnemonic, string[] args)> parsed_Code_B_;
        private bool use_Parsed_Code_A_;

        private readonly BidirectionalGraph<int, TaggedEdge<int, bool>> graph_;

        /// <summary>Constructor that creates an empty CFlow</summary>
        public StaticFlow(Tools tools)
        {
            //Console.WriteLine("INFO: CFlow: constructor");
            this.tools_ = tools;
            this.use_Parsed_Code_A_ = true;
            this.parsed_Code_A_ = new List<(string label, Mnemonic mnemonic, string[] args)>();
            this.parsed_Code_B_ = new List<(string label, Mnemonic mnemonic, string[] args)>();
            this.graph_ = new BidirectionalGraph<int, TaggedEdge<int, bool>>(true); // allowParallesEdges is true because of conditional jumps to the next line
        }

        #region Getters

        public StateConfig Create_StateConfig()
        {
            return this.Create_StateConfig(0, this.LastLineNumber - 1);
        }

        public StateConfig Create_StateConfig(
            int lineNumberBegin,
            int lineNumberEnd)
        {
            ISet<Rn> regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            bool mem = false;
            (string, string, string) dummyKeys = (string.Empty, string.Empty, string.Empty);
            for (int lineNumber = lineNumberBegin; lineNumber <= lineNumberEnd; lineNumber++)
            {
                (Mnemonic mnemonic, string[] args) content = this.Get_Line(lineNumber);
                using Mnemonics.OpcodeBase opcodeBase = Runner.InstantiateOpcode(content.mnemonic, content.args, dummyKeys, this.tools_);
                if (opcodeBase != null)
                {
                    flags |= opcodeBase.FlagsReadStatic | opcodeBase.FlagsWriteStatic;
                    foreach (Rn r in opcodeBase.RegsReadStatic)
                    {
                        regs.Add(RegisterTools.Get64BitsRegister(r));
                    }

                    foreach (Rn r in opcodeBase.RegsWriteStatic)
                    {
                        regs.Add(RegisterTools.Get64BitsRegister(r));
                    }

                    mem |= opcodeBase.MemWriteStatic || opcodeBase.MemReadStatic;
                }
            }

            StateConfig config = new();
            config.Set_All_Off();
            config.Set_Flags_On(flags);
            foreach (Rn reg in regs)
            {
                config.Set_Reg_On(reg);
            }

            config.Mem = mem;
            return config;
        }

        public string Get_Key(int lineNumber)
        {
            if (false)
            {
                return Tools.CreateKey(this.tools_.Rand);
            }
            else
            {
                return "!" + lineNumber.ToString();
            }
        }

        public (string key1, string key2) Get_Key((int lineNumber1, int lineNumber2) lineNumber)
        {
            if (true)
            {
                string key1 = this.Get_Key(lineNumber.lineNumber1);
                string key2 = this.Get_Key(lineNumber.lineNumber2);
                return (key1, key2);
            }
            else
            {
                string key1 = Tools.CreateKey(this.tools_.Rand);
                string key2 = key1 + "B";
                return (key1, key2);
            }
        }

        public int NLines { get { return this.Current.Count; } }

        public int FirstLineNumber
        {
            get
            {
                int lineNumber = 0;
                IList<(string label, Mnemonic mnemonic, string[] args)> current = this.Current;
                while (current[lineNumber].mnemonic == Mnemonic.NONE)
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

        public (Mnemonic mnemonic, string[] args) Get_Line(int lineNumber)
        {
            Contract.Requires(lineNumber >= 0);
            if (lineNumber >= this.Current.Count)
            {
                Console.WriteLine("WARING: CFlow:geLine: lineNumber " + lineNumber + " does not exist");
                return (Mnemonic.NONE, Array.Empty<string>());
            }
            (string label, Mnemonic mnemonic, string[] args) = this.Current[lineNumber];

            return (mnemonic, args);
        }

        public string Get_Line_Str(int lineNumber)
        {
            return this.HasLine(lineNumber) ? ToString(this.Current[lineNumber]) : string.Empty;
        }

        public bool Has_Prev_LineNumber(int lineNumber)
        {
            return !this.graph_.IsInEdgesEmpty(lineNumber);
        }

        public bool Has_Next_LineNumber(int lineNumber)
        {
            return !this.graph_.IsOutEdgesEmpty(lineNumber);
        }

        public int Number_Prev_LineNumbers(int lineNumber)
        {
            return this.graph_.InDegree(lineNumber);
        }

        public int Number_Next_LineNumbers(int lineNumber)
        {
            return this.graph_.OutDegree(lineNumber);
        }

        /// <summary>
        /// Get the previous line numbers and whether those line numbers branched to the current line number
        /// </summary>
        public IEnumerable<(int lineNumber, bool isBranch)> Get_Prev_LineNumber(int lineNumber)
        {
            foreach (TaggedEdge<int, bool> v in this.graph_.InEdges(lineNumber))
            {
                yield return (v.Source, v.Tag);
            }
        }

        public (int regular, int branch) Get_Next_LineNumber(int lineNumber)
        {
            int regular = -1;
            int branch = -1;

            foreach (TaggedEdge<int, bool> v in this.graph_.OutEdges(lineNumber))
            {
                if (v.Tag)
                {
                    branch = v.Target;
                }
                else
                {
                    regular = v.Target;
                }
            }
            return (regular, branch);
        }

        /// <summary>A LoopBranchPoint is a BranchPoint that choices between leaving the loop or staying in the loop.
        /// BranchToExitLoop is true if the branch code flow is used to leave the loop.</summary>
        public (bool isLoopBranchPoint, bool branchToExitLoop) Is_Loop_Branch_Point(int lineNumber)
        {
            if (this.Is_Branch_Point(lineNumber))
            {
                (int regular, int branch) = this.Get_Next_LineNumber(lineNumber);
                bool hasCodePath_Branch = this.HasCodePath(branch, lineNumber);
                bool hasCodePath_Regular = this.HasCodePath(regular, lineNumber);

                if (hasCodePath_Branch && !hasCodePath_Regular)
                {
                    return (isLoopBranchPoint: true, branchToExitLoop: false);
                }
                else if (!hasCodePath_Branch && hasCodePath_Regular)
                {
                    return (isLoopBranchPoint: true, branchToExitLoop: true);
                }
            }
            return (false, false);
        }

        /// <summary>A LoopMergePoint is a MergePoint that merges a loop with its begin point.</summary>
        public (bool isLoopMergePoint, int loopLineNumber) Is_Loop_Merge_Point(int lineNumber)
        {
            if (this.Is_Merge_Point(lineNumber))
            {
                int numberOfLoops = 0;
                int loopLineNumber = -1;
                // TODO return the smallest loop

                foreach ((int lineNumber1, bool isBranch) in this.Get_Prev_LineNumber(lineNumber))
                {
                    if (this.HasCodePath(lineNumber, lineNumber1))
                    {
                        numberOfLoops++;
                        loopLineNumber = lineNumber1;
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
                if (lineNumber2 == -1)
                {
                    return;
                }

                if (this.HasLine(lineNumber2) && !result.Contains(lineNumber2))
                {
                    result.Add(lineNumber2);
                    (int regular, int branch) = this.Get_Next_LineNumber(lineNumber2);
                    FutureLineNumbers_Local(regular);
                    FutureLineNumbers_Local(branch);
                }
            }
        }

        /// <summary>A BranchPoint is an code line that has two next states (that need not be different)</summary>
        public bool Is_Branch_Point(int lineNumber)
        {
            return this.graph_.OutDegree(lineNumber) > 1;
        }

        /// <summary>A MergePoint is a code line which has at least two control flows that merge into this line</summary>
        public bool Is_Merge_Point(int lineNumber)
        {
            return this.graph_.InDegree(lineNumber) > 1;
        }

        #endregion

        #region Setters

        /// <summary>Update this CFlow with the provided programStr: return true if this CFlow has changed.</summary>
        public bool Update(string programStr, bool removeEmptyLines = true)
        {
            Contract.Requires(programStr != null);

            //Console.WriteLine("INFO: CFlow:Update_Lines");
            this.use_Parsed_Code_A_ = !this.use_Parsed_Code_A_;

            #region Parse to find all labels
            IDictionary<string, int> labels = GetLabels(programStr);
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
                Contract.Assert(programStr != null);
            }
            #endregion

            IList<(string label, Mnemonic mnemonic, string[] args)> previous = this.Previous;
            IList<(string label, Mnemonic mnemonic, string[] args)> current = this.Current;

            current.Clear();
            this.graph_.Clear();

            #region Populate IncomingLines
            {
                string[] lines = programStr.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

                for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
                {
                    (KeywordID[] _, string label, Mnemonic mnemonic, string[] args, string remark) line = AsmSourceTools.ParseLine(lines[lineNumber], -1, -1);
                    EvalArgs(ref line.args);
                    current.Add((line.label, line.mnemonic, line.args));

                    (int jumpTo1, int jumpTo2) = Static_Jump(line.mnemonic, line.args, lineNumber);
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

            if (current.Count != previous.Count)
            {
                equal = false;
            }
            else
            {
                for (int lineNumber = 0; lineNumber < current.Count; ++lineNumber)
                {
                    (string label, Mnemonic mnemonic, string[] args) previous_line = previous[lineNumber];
                    (string label, Mnemonic mnemonic, string[] args) current_line = current[lineNumber];
                    if (previous_line.label != current_line.label)
                    {
                        equal = false;
                        break;
                    }
                    else if (previous_line.mnemonic != current_line.mnemonic)
                    {
                        equal = false;
                        break;
                    }
                    else if (!Enumerable.SequenceEqual(previous_line.args, current_line.args))
                    {
                        equal = false;
                        break;
                    }
                }
            }
            return !equal;
            #endregion
        }

        private static void EvalArgs(ref string[] args)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                (bool valid, ulong value, int nBits) = ExpressionEvaluator.Evaluate_Constant(args[i]);
                if (valid)
                {
                    args[i] = value.ToString();
                }
            }
        }

        /// <summary>Compress this static flow by removing empty lines</summary>
        private void Compress()
        {
            IList<(string label, Mnemonic mnemonic, string[] args)> current = this.Current;
            for (int lineNumber = 0; lineNumber < current.Count; ++lineNumber)
            {
                (string label, Mnemonic mnemonic, string[] args) c = current[lineNumber];

                if (c.mnemonic == Mnemonic.NONE) // found an empty line
                {
                    int outDegree = this.graph_.OutDegree(lineNumber);
                    if (outDegree == 0)
                    {
                        this.graph_.RemoveVertex(lineNumber);
                    }
                    else if (outDegree == 1)
                    {
                        TaggedEdge<int, bool> outEdge = this.graph_.OutEdge(lineNumber, 0);
                        this.graph_.RemoveEdge(outEdge);
                        int next = outEdge.Target;

                        //Remove this empty line
                        List<TaggedEdge<int, bool>> inEdges = new(this.graph_.InEdges(lineNumber));
                        foreach (TaggedEdge<int, bool> e in inEdges)
                        {
                            this.graph_.AddEdge(new TaggedEdge<int, bool>(e.Source, next, e.Tag));
                            this.graph_.RemoveEdge(e);
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
            string arguments = string.Empty;

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
                return string.Format(Culture, "{0}:", t.label);
            }
            else
            {
                return string.Format(Culture, "{0}{1} {2}", (t.label.Length > 0) ? (t.label + ": ") : string.Empty, t.mnemonic, arguments);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new();

            for (int i = 0; i < this.Current.Count; ++i)
            {
                sb.Append("Line " + i + ": ");
                sb.Append(this.Get_Line_Str(i));
                sb.Append(" [Prev:");
                foreach ((int lineNumber, bool isBranch) in this.Get_Prev_LineNumber(i))
                {
                    sb.Append(lineNumber + (isBranch ? "B" : "R") + ",");  // B=Branching; R=Regular Continuation
                }
                sb.Append("][Next:");
                (int regular, int branch) = this.Get_Next_LineNumber(i);
                if (regular != -1)
                {
                    sb.Append(regular + "R,");
                }

                if (branch != -1)
                {
                    sb.Append(branch + "B");
                }

                sb.AppendLine("]");
            }
            return sb.ToString();
        }

        #endregion ToString Methods

        #region Private Methods

        private IList<(string label, Mnemonic mnemonic, string[] args)> Current
        {
            get { return this.use_Parsed_Code_A_ ? this.parsed_Code_A_ : this.parsed_Code_B_; }
        }

        private IList<(string label, Mnemonic mnemonic, string[] args)> Previous
        {
            get { return this.use_Parsed_Code_A_ ? this.parsed_Code_B_ : this.parsed_Code_A_; }
        }

        private void Add_Edge(int jumpFrom, int jumpTo, bool isBranch)
        {
            //Console.WriteLine("INFO: from " + jumpFrom + " to " + jumpTo + " (branch " + isBranch + ")");
            if (!this.graph_.ContainsVertex(jumpFrom))
            {
                this.graph_.AddVertex(jumpFrom);
            }

            if (!this.graph_.ContainsVertex(jumpTo))
            {
                this.graph_.AddVertex(jumpTo);
            }

            //if (this._graph.ContainsEdge(jumpFrom, jumpTo)) {
            //    Console.WriteLine("INFO: Edge already exists: from " + jumpFrom + " to " + jumpTo + " (branch " + isBranch + ")");
            //}

            this.graph_.AddEdge(new TaggedEdge<int, bool>(jumpFrom, jumpTo, isBranch));
        }

        private static (int regularLineNumber, int branchLineNumber) Static_Jump(Mnemonic mnemonic, string[] args, int lineNumber)
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
        private static IDictionary<string, int> GetLabels(string text)
        {
            Contract.Requires(text != null);

            IDictionary<string, int> result = new Dictionary<string, int>();
            string[] lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int lineNumber = 0; lineNumber < lines.Length; ++lineNumber)
            {
                string line = lines[lineNumber];
                (bool valid, int beginPos, int endPos) = AsmSourceTools.GetLabelDefPos(line);
                if (valid)
                {
                    int labelBeginPos = beginPos;
                    int labelEndPos = endPos;
                    string label = line.Substring(labelBeginPos, labelEndPos - labelBeginPos);
                    if (result.ContainsKey(label))
                    {
                        Console.WriteLine(string.Format(Culture, "WARNING: getLabels: found a clashing label \"{0}\" at line=\"{1}\".", label, lineNumber));
                    }
                    else
                    {
                        result.Add(label, lineNumber);
                    }
                    //Console.WriteLine(string.Format(AsmDudeToolsStatic.CultureUI, "INFO: getLabels: label=\"{0}\"; line=\"{1}\".", label, lineNumber));
                }
            }
            return result;
        }
        #endregion Private Methods
    }
}
