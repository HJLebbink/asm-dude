// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

namespace AsmSim
{
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
    using System.Diagnostics.Contracts;
    using System.Text;
    using AsmTools;
    using Microsoft.Z3;
    using QuickGraph;

    public class DynamicFlow : IDisposable
    {
        #region Fields
        private readonly Tools _tools;

        private readonly BidirectionalGraph<string, TaggedEdge<string, (bool branch, StateUpdate stateUpdate)>> _graph;
        private readonly IDictionary<int, string> _lineNumber_2_Key;
        private readonly IDictionary<string, int> _key_2_LineNumber;
        private string _rootKey;
        private readonly object _updateLock = new object();
        #endregion

        #region Constructors
        public DynamicFlow(Tools tools)
        {
            this._tools = tools;
            this._graph = new BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>>(true); // allowParallelEdges because of conditional branches to the next line of code
            this._lineNumber_2_Key = new Dictionary<int, string>();
            this._key_2_LineNumber = new Dictionary<string, int>();
        }
        #endregion

        #region Getters

        public BidirectionalGraph<string, TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> Graph { get { return this._graph; } }

        public bool Is_Branch_Point(int lineNumber)
        {
            string key = this.Key(lineNumber);
            if (this.Has_Vertex(key))
            {
                return this._graph.OutDegree(key) > 1;
            }
            return false;
        }

        public bool Is_Merge_Point(int lineNumber)
        {
            string key = this.Key(lineNumber);
            if (this.Has_Vertex(key))
            {
                return this._graph.InDegree(key) > 1;
            }
            return false;
        }

        public string Key(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out string key))
            {
                return key;
            }
            return "NOKEY";
        }

        public string Key_Previous(int lineNumber)
        {
            string key = this.Key(lineNumber);
            if (this.Has_Vertex(key))
            {
                switch (this._graph.InDegree(key))
                {
                    case 0:
                        Console.WriteLine("WARNING: DynamicFlow: Key_Previous: no previous key");
                        return "NOKEY";
                    case 1:
                        return this._graph.InEdge(key, 0).Source;
                    default:
                        Console.WriteLine("WARNING: DynamicFlow: Key_Previous: multiple previous keys, returning the first one");
                        return this._graph.InEdge(key, 0).Source;
                }
            }
            return "NOKEY";
        }

        private IEnumerable<StateUpdate> Get_Incoming_StateUpdate(string key)
        {
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in this._graph.InEdges(key))
            {
                yield return v.Tag.StateUpdate;
            }
        }

        private IEnumerable<StateUpdate> Get_Outgoing_StateUpdate(string key)
        {
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in this._graph.OutEdges(key))
            {
                yield return v.Tag.StateUpdate;
            }
        }

        public string Key_Next(int lineNumber)
        {
            string key = this.Key(lineNumber);
            if (this.Has_Vertex(key))
            {
                switch (this._graph.OutDegree(key))
                {
                    case 0:
                        Console.WriteLine("WARNING: DynamicFlow: Key_Next: no next key");
                        return "NOKEY";
                    case 1:
                        return this._graph.OutEdge(key, 0).Target;
                    default:
                        Console.WriteLine("WARNING: DynamicFlow: Key_Next: multiple next keys, returning the first one");
                        return this._graph.OutEdge(key, 0).Target;
                }
            }
            return "NOKEY";
        }

        private bool Has_Vertex(string key)
        {
            return this._graph.ContainsVertex(key);
        }

        private bool Has_Edge(string source, string target, bool isBranch)
        {
            if (this._graph.TryGetEdge(source, target, out TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> tag))
            {
                return tag.Tag.Branch == isBranch;
            }
            return false;
        }

        public bool Has_LineNumber(int lineNumber)
        {
            return this._lineNumber_2_Key.ContainsKey(lineNumber);
        }

        public int LineNumber(string key)
        {
            return this._key_2_LineNumber.TryGetValue(key, out int v) ? v : -1;
        }

        public IEnumerable<State> Create_States_Before(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out string key))
            {
                yield return this.Create_State_Private(key, false);
            }
        }

        public State Create_States_Before(int lineNumber, int index)
        {
            int counter = 0;
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out string key))
            {
                if (index == counter)
                {
                    return this.Create_State_Private(key, false);
                }

                counter++;
            }
            return null;
        }

        public IEnumerable<State> Create_States_After(int lineNumber)
        {
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out string key))
            {
                yield return this.Create_State_Private(key, true);
            }
        }

        public State Create_States_After(int lineNumber, int index)
        {
            int counter = 0;
            if (this._lineNumber_2_Key.TryGetValue(lineNumber, out string key))
            {
                if (index == counter)
                {
                    return this.Create_State_Private(key, true);
                }

                counter++;
            }
            return null;
        }

        public State Create_State_After(string key)
        {
            if (!this._graph.ContainsVertex(key))
            {
                return null;
            }

            return this.Create_State_Private(key, true);
        }

        public State Create_State_Before(string key)
        {
            if (!this._graph.ContainsVertex(key))
            {
                return null;
            }

            return this.Create_State_Private(key, false);
        }

        private bool Has_Branch(int lineNumber)
        {
            string key = this._lineNumber_2_Key[lineNumber];
            return this._graph.OutDegree(key) > 1;
        }

        public BoolExpr Get_Branch_Condition(int lineNumber)
        {
            string key = this._lineNumber_2_Key[lineNumber];
            return this._graph.OutEdge(key, 0).Tag.stateUpdate.BranchInfo.BranchCondition;
        }

        /// <summary> Create leafs of this DynamicFlow</summary>
        public IEnumerable<State> Leafs
        {
            get
            {
                HashSet<string> alreadyVisisted = new HashSet<string>();
                foreach (string key in Get_Leafs_LOCAL(this._rootKey))
                {
                    yield return this.Create_State_Private(key, true);
                }

                #region Local Methods
                IEnumerable<string> Get_Leafs_LOCAL(string key)
                {
                    if (alreadyVisisted.Contains(key))
                    {
                        yield break;
                    }

                    alreadyVisisted.Add(key);

                    if (this.Has_Vertex(key))
                    {
                        if (this._graph.IsOutEdgesEmpty(key))
                        {
                            yield return key;
                        }
                        else
                        {
                            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge in this._graph.OutEdges(key))
                            {
                                foreach (string v in Get_Leafs_LOCAL(edge.Target))
                                {
                                    yield return v;
                                }
                            }
                        }
                    }
                }
                #endregion
            }
        }

        public State EndState
        {
            get
            {
                IEnumerable<State> leafs = this.Leafs;
                State result = Tools.Collapse(leafs);
                foreach (State v in leafs)
                {
                    v.Dispose();
                }

                return result;
            }
        }

        #endregion

        #region Setters

        public void Clear()
        {
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in this._graph.Edges)
            {
                v.Tag.StateUpdate.Dispose();
            }

            this._graph.Clear();
            this._lineNumber_2_Key.Clear();
            this._key_2_LineNumber.Clear();
        }

        public void Reset(StaticFlow sFlow, bool forward)
        {
            Contract.Requires(sFlow != null);

            lock (this._updateLock)
            {
                this._rootKey = "!" + sFlow.FirstLineNumber;
                this.Clear();

                if (forward)
                {
                    this.Update_Forward(sFlow, sFlow.FirstLineNumber);
                }
                else
                {
                    this.Update_Backward(sFlow, sFlow.LastLineNumber);
                }
            }
        }

        private void Update_Forward(StaticFlow sFlow, int startLineNumber)
        {
            if (!sFlow.HasLine(startLineNumber))
            {
                if (!this._tools.Quiet)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Update_Forward: startLine " + startLineNumber + " does not exist in " + sFlow);
                }

                return;
            }
            Stack<string> nextKeys = new Stack<string>();

            // Get the head of the current state, this head will be the prevKey of the update, nextKey is fresh.
            // When state is updated, tail is not changed; head is set to the fresh nextKey.

            #region Create the Root node
            {
                string rootKey = sFlow.Get_Key(startLineNumber);
                nextKeys.Push(rootKey);
                this.Add_Vertex(rootKey, startLineNumber);
            }
            #endregion

            while (nextKeys.Count > 0)
            {
                string prevKey = nextKeys.Pop();
                int currentLineNumber = this.LineNumber(prevKey);
                if (sFlow.HasLine(currentLineNumber))
                {
                    (int Regular, int Branch) nextLineNumber = sFlow.Get_Next_LineNumber(currentLineNumber);
                    (string nextKey, string nextKeyBranch) = sFlow.Get_Key(nextLineNumber);

                    (StateUpdate regular, StateUpdate branch) = Runner.Execute(sFlow, currentLineNumber, (prevKey, nextKey, nextKeyBranch), this._tools);

                    HandleBranch_LOCAL(currentLineNumber, nextLineNumber.Branch, branch, prevKey, nextKeyBranch);
                    HandleRegular_LOCAL(currentLineNumber, nextLineNumber.Regular, regular, prevKey, nextKey);
                }
            }
            #region Local Methods
            void HandleBranch_LOCAL(int currentLineNumber, int nextLineNumber, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        //Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a branch yet a branch is computed");
                        return;
                    }
                    if (!this.Has_Edge(prevKey, nextKey, true))
                    {
                        this.Add_Vertex(nextKey, nextLineNumber);
                        this.Add_Edge(true, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!this._tools.Quiet)
                        {
                            Console.WriteLine("=====================================");
                        }

                        if (!this._tools.Quiet)
                        {
                            Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: LINE " + currentLineNumber + ": \"" + sFlow.Get_Line_Str(currentLineNumber) + "\" Branches to LINE " + nextLineNumber);
                        }

                        if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).mnemonic != Mnemonic.NONE)
                        {
                            Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        }
                        //if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.UNKNOWN) Console.WriteLine("INFO: " + this.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    //Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a branch yet no branch is computed");
                    return;
                }
            }
            void HandleRegular_LOCAL(int currentLineNumber, int nextLineNumber, StateUpdate update, string prevKey, string nextKey)
            {
                if (update != null)
                {
                    if (nextLineNumber == -1)
                    {
                        Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there does not exists a continue yet a continue is computed");
                    }
                    if (!this.Has_Edge(prevKey, nextKey, false))
                    {
                        this.Add_Vertex(nextKey, nextLineNumber);
                        this.Add_Edge(false, update, prevKey, nextKey);
                        nextKeys.Push(nextKey);

                        #region Display
                        if (!this._tools.Quiet)
                        {
                            Console.WriteLine("=====================================");
                        }

                        if (!this._tools.Quiet)
                        {
                            Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: LINE " + currentLineNumber + ": \"" + sFlow.Get_Line_Str(currentLineNumber) + "\" Continues to LINE " + nextLineNumber);
                        }

                        if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).mnemonic != Mnemonic.NONE)
                        {
                            Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Forward: " + update);
                        }
                        //if (!this._tools.Quiet && sFlow.Get_Line(currentLineNumber).Mnemonic != Mnemonic.NONE) Console.WriteLine("INFO: " + this.State_After(nextKey));
                        #endregion
                    }
                }
                else if (nextLineNumber != -1)
                {
                    Console.WriteLine("WARNING: Runner:Construct_DynamicFlow_Forward: according to flow there exists a regular continue yet no continue is computed");
                }
            }

            #endregion
        }

        private void Update_Backward(StaticFlow sFlow, int startLineNumber)
        {
            if (!sFlow.Has_Prev_LineNumber(startLineNumber))
            {
                if (!this._tools.Quiet)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Update_Backward startLine " + startLineNumber + " does not have a previous line in " + sFlow);
                }

                return;
            }
            Stack<string> prevKeys = new Stack<string>();

            // Get the tail of the current state, this tail will be the nextKey, the prevKey is fresh.
            // When the state is updated, the head is unaltered, tail is set to the fresh prevKey.

            #region Create the Root node
            string rootKey = sFlow.Get_Key(startLineNumber);
            prevKeys.Push(rootKey);
            this.Add_Vertex(rootKey, startLineNumber);
            #endregion

            while (prevKeys.Count > 0)
            {
                string nextKey = prevKeys.Pop();

                int currentLineNumber = this.LineNumber(nextKey);

                foreach ((int LineNumber, bool IsBranch) prev in sFlow.Get_Prev_LineNumber(currentLineNumber))
                {
                    if (sFlow.HasLine(prev.LineNumber))
                    {
                        string prevKey = sFlow.Get_Key(prev.LineNumber);
                        if (!this.Has_Edge(prevKey, nextKey, prev.IsBranch))
                        {
                            (StateUpdate regular, StateUpdate branch) = Runner.Execute(sFlow, prev.LineNumber, (prevKey, nextKey, nextKey), this._tools);
                            StateUpdate update = null;
                            if (prev.IsBranch)
                            {
                                update = branch;
                                regular?.Dispose();
                            }
                            else
                            {
                                update = regular;
                                branch?.Dispose();
                            }

                            this.Add_Vertex(prevKey, prev.LineNumber);
                            this.Add_Edge(prev.IsBranch, update, prevKey, nextKey);

                            //Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: scheduling key " + prevKey);
                            prevKeys.Push(prevKey); // only continue if the state is consistent; no need to go futher in the past if the state is inconsistent.

                            #region Display
                            if (!this._tools.Quiet)
                            {
                                Console.WriteLine("=====================================");
                            }

                            if (!this._tools.Quiet)
                            {
                                Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: LINE " + prev.LineNumber + ": \"" + sFlow.Get_Line_Str(prev.LineNumber) + "; branch=" + prev.IsBranch);
                            }

                            if (!this._tools.Quiet && sFlow.Get_Line(prev.LineNumber).mnemonic != Mnemonic.NONE)
                            {
                                Console.WriteLine("INFO: Runner:Construct_DynamicFlow_Backward: " + update);
                            }
                            //if (!tools.Quiet && flow.GetLine(prev_LineNumber).Mnemonic != Mnemonic.NONE) Console.WriteLine("INFO: " + stateTree.State_After(rootKey));
                            #endregion
                        }
                    }
                }
            }
        }

        private void Add_Vertex(string key, int lineNumber)
        {
            if (!this._graph.ContainsVertex(key))
            {
                this._graph.AddVertex(key);
                this._key_2_LineNumber.Add(key, lineNumber);
                if (this._lineNumber_2_Key.ContainsKey(lineNumber))
                {
                    if (this._lineNumber_2_Key[lineNumber] != key)
                    {
                        Console.WriteLine("WARNING: DynamicFlow: Add_Vertex: lineNumber " + lineNumber + " already has a key");
                    }
                }
                else
                {
                    this._lineNumber_2_Key.Add(lineNumber, key);
                }
            }
        }

        private void Add_Edge(bool isBranch, StateUpdate stateUpdate, string source, string target)
        {
            if (this._graph.TryGetEdge(source, target, out TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> tag))
            {
                if (tag.Tag.Branch == isBranch)
                {
                    Console.WriteLine("WARNING: DynamicFlow.Add_Edge: edge " + source + "->" + target + " with branch=" + isBranch + " already exists");
                    return;
                }
            }
            //Console.WriteLine("INFO: DynamicFlow.Add_Edge: adding edge " + source + "->" + target + " with branch=" + isBranch + ".");
            this._graph.AddEdge(new TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>(source, target, (isBranch, stateUpdate)));
        }

        #endregion

        #region ToString
        public override string ToString()
        {
            return this.ToString(null);
        }

        public string ToString(StaticFlow flow)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, int> k in this._key_2_LineNumber)
            {
                sb.AppendLine("Key " + k.Key + " -> LineNumber " + k.Value);
            }

            this.ToString(this._rootKey, flow, ref sb);
            return sb.ToString();
        }

        private void ToString(string key, StaticFlow sFlow, ref StringBuilder sb)
        {
            if (!this.Has_Vertex(key))
            {
                return;
            }

            int lineNumber = this.LineNumber(key);
            string codeLine = (sFlow == null) ? string.Empty : sFlow.Get_Line_Str(lineNumber);

            sb.AppendLine("==========================================");
            using (State v = this.Create_State_Private(key, true))
            {
                sb.AppendLine("State " + key + ": " + v?.ToString());
            }
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in this._graph.OutEdges(key))
            {
                Debug.Assert(v.Source == key);
                string nextKey = v.Target;
                sb.AppendLine("------------------------------------------");
                sb.AppendLine("Transition from state " + key + " to " + nextKey + "; execute LINE " + lineNumber + ": \"" + codeLine + "\" " + (v.Tag.Branch ? "[Forward Branching]" : "[Forward Continue]"));
                this.ToString(nextKey, sFlow, ref sb);
            }
        }

        public string ToStringOverview(StaticFlow flow, bool showRegisterValues = false)
        {
            return "TODO";
        }
        #endregion

        #region Private Methods

        private State Create_State_Private(string key, bool after)
        {
            List<string> visisted = new List<string>();
            lock (this._updateLock)
            {
                State result = Construct_State_Private_LOCAL(key, after, visisted) ?? new State(this._tools, key, key);
                result.Frozen = true;
                return result;
            }

            #region Local Methods
            State Construct_State_Private_LOCAL(string key_LOCAL, bool after_LOCAL, ICollection<string> visited_LOCAL)
            {
                #region Payload
                if (visited_LOCAL.Contains(key_LOCAL)) // found a cycle
                {
                    Console.WriteLine("WARNING: DynamicFlow: Construct_State_Private: Found cycle at key " + key_LOCAL + "; not implemented yet.");
                    return null; //new State(tools, key_LOCAL, key_LOCAL);
                }
                if (!this.Has_Vertex(key_LOCAL))
                {
                    Console.WriteLine("WARNING: DynamicFlow: Construct_State_Private: key " + key_LOCAL + " not found.");
                    return new State(this._tools, key_LOCAL, key_LOCAL);
                }

                State result;
                visited_LOCAL.Add(key_LOCAL);

                switch (this._graph.InDegree(key_LOCAL))
                {
                    case 0:
                        {
                            result = new State(this._tools, key_LOCAL, key_LOCAL);
                            break;
                        }
                    case 1:
                        {
                            TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge = this._graph.InEdge(key_LOCAL, 0);
                            if (edge.Tag.StateUpdate.Reset)
                            {
                                result = new State(this._tools, key_LOCAL, key_LOCAL);
                            }
                            else
                            {
                                result = Construct_State_Private_LOCAL(edge.Source, false, visited_LOCAL); // recursive call
                                if (result == null)
                                {
                                    return null;
                                }

                                result.Update_Forward(edge.Tag.StateUpdate);
                            }
                            break;
                        }
                    default:
                        {
                            (string Source, StateUpdate StateUpdate) incoming_Regular = this.Get_Regular(this._graph.InEdges(key_LOCAL));
                            IEnumerable<(string Source, StateUpdate StateUpdate)> incoming_Branches = this.Get_Branches(this._graph.InEdges(key_LOCAL));
                            result = Merge_State_Update_LOCAL(key_LOCAL, incoming_Regular, incoming_Branches, visited_LOCAL);
                            if (result == null)
                            {
                                return null;
                            }

                            break;
                        }
                }

                if (after_LOCAL)
                {
                    switch (this._graph.OutDegree(key_LOCAL))
                    {
                        case 0:
                            break;
                        case 1:
                            TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge = this._graph.OutEdge(key_LOCAL, 0);
                            result.Update_Forward(edge.Tag.StateUpdate);
                            break;
                        case 2:
                            using (State state1 = new State(result))
                            using (State state2 = new State(result))
                            {
                                TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge1 = this._graph.OutEdge(key_LOCAL, 0);
                                state1.Update_Forward(edge1.Tag.StateUpdate);

                                TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge2 = this._graph.OutEdge(key_LOCAL, 1);
                                state2.Update_Forward(edge2.Tag.StateUpdate);

                                result = new State(state1, state2, true);
                            }
                            break;
                        default:
                            // unreachable:
                            Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private: OutDegree = " + this._graph.OutDegree(key_LOCAL) + " is not implemented yet");
                            result = new State(this._tools, key_LOCAL, key_LOCAL);
                            break;
                    }
                }
                if (result == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private: Returning null!");
                }
                #endregion

                return result;
            }

            State Merge_State_Update_LOCAL(
                string target,
                (string Source, StateUpdate StateUpdate) incoming_Regular,
                IEnumerable<(string Source, StateUpdate StateUpdate)> incoming_Branches,
                ICollection<string> visited2)
            {
                string source1 = incoming_Regular.Source;
                using (State state1 = Construct_State_Private_LOCAL(source1, false, new List<string>(visited2)))
                {
                    if (state1 == null)
                    {
                        return null;
                    }

                    string nextKey1 = target + "A0";
                    {
                        StateUpdate update1 = incoming_Regular.StateUpdate;
                        update1.NextKey = nextKey1;
                        state1.Update_Forward(update1);
                    }
                    State result_State = new State(this._tools, state1.TailKey, state1.HeadKey);

                    IList<StateUpdate> mergeStateUpdates = new List<StateUpdate>();
                    HashSet<BoolExpr> tempSet1 = new HashSet<BoolExpr>();
                    HashSet<BoolExpr> tempSet2 = new HashSet<BoolExpr>();
                    HashSet<string> sharedBranchConditions = new HashSet<string>();
                    List<BranchInfo> allBranchConditions = new List<BranchInfo>();

                    foreach (BoolExpr v1 in state1.Solver.Assertions)
                    {
                        tempSet1.Add(v1);
                    }

                    foreach (BoolExpr v1 in state1.Solver_U.Assertions)
                    {
                        tempSet2.Add(v1);
                    }

                    foreach (BranchInfo v1 in state1.BranchInfoStore.Values)
                    {
                        allBranchConditions.Add(v1);
                    }

                    int counter = 0;
                    List<(string Source, StateUpdate StateUpdate)> incoming_Branches_list = new List<(string Source, StateUpdate StateUpdate)>(incoming_Branches);
                    incoming_Branches_list.Reverse(); //TODO does this always works??
                    int nBranches = incoming_Branches_list.Count;
                    foreach ((string Source, StateUpdate StateUpdate) incoming_Branch in incoming_Branches_list)
                    {
                        string source2 = incoming_Branch.Source;
                        using (State state2 = Construct_State_Private_LOCAL(source2, false, new List<string>(visited2)))
                        { // recursive call
                            if (state2 == null)
                            {
                                return null;
                            }

                            string nextKey2 = target + "B" + counter;
                            counter++;
                            {
                                StateUpdate update2 = incoming_Branch.StateUpdate;
                                update2.NextKey = nextKey2; //TODO BUG here, is the reference updated???
                                state2.Update_Forward(update2);
                            }

                            BoolExpr bc = null;
                            {
                                using (Context ctx = new Context(this._tools.Settings))
                                {
                                    string branchKey = GraphTools<(bool, StateUpdate)>.Get_Branch_Point(source1, source2, this._graph);
                                    BranchInfo branchInfo = Get_Branch_Condition_LOCAL(branchKey);
                                    if (branchInfo == null)
                                    {
                                        Console.WriteLine("WARNING: DynamicFlow:Construct_State_Private:GetStates_LOCAL: branchInfo is null. source1=" + source1 + "; source2=" + source2);
                                        bc = ctx.MkBoolConst("BC" + target);
                                    }
                                    else
                                    {
                                        bc = branchInfo.BranchCondition;
                                        sharedBranchConditions.Add(bc.ToString());
                                    }
                                }
                                string nextKey3 = (counter == nBranches) ? target : target + "A" + counter;

                                using (StateUpdate stateUpdate = new StateUpdate(bc, nextKey2, nextKey1, nextKey3, this._tools))
                                {
                                    nextKey1 = nextKey3;
                                    mergeStateUpdates.Add(stateUpdate);
                                }
                            }
                            if (state1.TailKey != state2.TailKey)
                            {
                                Console.WriteLine("WARNING: DynamicFlow: Merge_State_Update_LOCAL: tails are unequal: tail1=" + state1.TailKey + "; tail2=" + state2.TailKey);
                            }
                            { // merge the states state1 and state2 into state3
                                foreach (BoolExpr v1 in state2.Solver.Assertions)
                                {
                                    tempSet1.Add(v1);
                                }

                                foreach (BoolExpr v1 in state2.Solver_U.Assertions)
                                {
                                    tempSet2.Add(v1);
                                }

                                foreach (BranchInfo v1 in state2.BranchInfoStore.Values)
                                {
                                    allBranchConditions.Add(v1);
                                }
                            }
                        }
                    }
                    result_State.Assert(tempSet1, false, true);
                    result_State.Assert(tempSet2, true, true);

                    foreach (BranchInfo v1 in allBranchConditions)
                    {
                        if (!sharedBranchConditions.Contains(v1.BranchCondition.ToString()))
                        {
                            result_State.Add(v1);
                        }
                    }
                    foreach (StateUpdate v1 in mergeStateUpdates)
                    {
                        result_State.Update_Forward(v1);
                        v1.Dispose();
                    }
                    return result_State;
                }
            }

            BranchInfo Get_Branch_Condition_LOCAL(string branchKey)
            {
                if (branchKey == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: BranchKey is null;");
                    return null;
                }
                if (this._graph.OutDegree(branchKey) != 2)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: incorrect out degree;");
                    return null;
                }
                TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge1 = this._graph.OutEdge(branchKey, 0);
                TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> edge2 = this._graph.OutEdge(branchKey, 1);

                if (edge1.Tag.StateUpdate.BranchInfo == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: branchinfo of edge1 is null");
                    return null;
                }
                if (edge2.Tag.StateUpdate.BranchInfo == null)
                {
                    Console.WriteLine("WARNING: DynamicFlow:Get_Branch_Condition: branchinfo of edge2 is null");
                    return null;
                }
                return edge1.Tag.StateUpdate.BranchInfo;
            }
            #endregion
        }

        private (string Source, StateUpdate StateUpdate) Get_Regular(IEnumerable<TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> inEdges)
        {
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in inEdges)
            {
                if (!v.Tag.Branch)
                {
                    return (v.Source, v.Tag.StateUpdate);
                }
            }

            return ("NOKEY", null);
        }

        private IEnumerable<(string Source, StateUpdate StateUpdate)> Get_Branches(IEnumerable<TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)>> inEdges)
        {
            foreach (TaggedEdge<string, (bool Branch, StateUpdate StateUpdate)> v in inEdges)
            {
                if (v.Tag.Branch)
                {
                    yield return (v.Source, v.Tag.StateUpdate);
                }
            }
        }
        #endregion

        #region IDisposable Support
        public void Dispose()
        {
            this.Clear();
        }
        #endregion
    }
}
