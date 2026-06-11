// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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
    using System.Linq;

    using AsmSim.Mnemonics;

    using AsmTools;

    using QuikGraph;

    /// <summary>
    /// Forward, single-pass evaluator over one built <see cref="DynamicFlow"/> (one CFG component). It
    /// computes a before-state per graph vertex by propagating along forward edges and joining at merges,
    /// resolving loops (back-edges) through the configured <see cref="ILoopStrategy"/>
    /// (<see cref="Tools.LoopHandling"/>). Computes every vertex once (no per-line re-walk) and does not
    /// bail on cycles.
    ///
    /// Every vertex's state is kept CLEAN and planted at the vertex's canonical key (so out-edges read it),
    /// and joins are done at the <see cref="Tv"/> level (per-bit), not by solver-merging states that share a
    /// canonical key (which would AND to a contradiction). The join is therefore value-level — slightly less
    /// path-sensitive than the legacy merge, but it drops provably-unreachable predecessors, and on
    /// straight-line code the two agree exactly. Nested/irreducible loop headers fall back to
    /// <see cref="LoopHandling.Accept"/> (sound) and are logged.
    /// </summary>
    public sealed class ComponentEvaluator : IDisposable
    {
        private readonly DynamicFlow flow_;
        private readonly StaticFlow sFlow_;
        private readonly Tools tools_;
        private readonly ILoopStrategy strategy_;

        private readonly List<string> topo_ = [];               // reverse-postorder over forward edges
        private readonly HashSet<(string src, string dst)> backEdges_ = [];
        private readonly HashSet<string> headers_ = [];          // back-edge targets
        private readonly Dictionary<string, State> before_ = []; // owned, clean, planted at vertex key
        private readonly Dictionary<string, State> after_ = [];  // owned; before + the line's instruction
        private int keyCounter_;

        public int LoopCount { get; private set; }
        public int LoopIterations { get; private set; }

        public ComponentEvaluator(DynamicFlow flow, StaticFlow sFlow)
        {
            this.flow_ = flow ?? throw new ArgumentNullException(nameof(flow));
            this.sFlow_ = sFlow ?? throw new ArgumentNullException(nameof(sFlow));
            this.tools_ = flow.FlowTools; // carries SharedCtx (= flow context), StateConfig, LoopHandling
            this.strategy_ = LoopStrategies.For(this.tools_.LoopHandling);

            this.ClassifyEdges();
            this.Evaluate();
        }

        /// <summary>Before-state of the given source line, or null if the line is not a graph vertex.
        /// Owned by this evaluator — do not dispose.</summary>
        public State? Before(int lineNumber)
        {
            string key = this.flow_.Key(lineNumber);
            return this.before_.TryGetValue(key, out State? s) ? s : null;
        }

        /// <summary>After-state of the given source line (before-state + the line's instruction), or null if
        /// the line is not a graph vertex. Owned by this evaluator — do not dispose.</summary>
        public State? After(int lineNumber)
        {
            string key = this.flow_.Key(lineNumber);
            return this.after_.TryGetValue(key, out State? s) ? s : null;
        }

        private BidirectionalGraph<string, TaggedEdge<string, (bool branch, StateUpdate stateUpdate)>> G => this.flow_.Graph;

        #region Graph classification (DFS: topo order + back-edges)
        private void ClassifyEdges()
        {
            var color = new Dictionary<string, int>(); // 0=white,1=gray,2=black
            foreach (string v in this.G.Vertices)
            {
                color[v] = 0;
            }

            var postorder = new List<string>();

            var roots = new List<string>();
            foreach (string v in this.G.Vertices)
            {
                if (this.G.IsInEdgesEmpty(v))
                {
                    roots.Add(v);
                }
            }
            foreach (string v in this.G.Vertices)
            {
                if (!roots.Contains(v))
                {
                    roots.Add(v); // ensure every vertex is eventually a DFS start (pure-cycle components)
                }
            }

            foreach (string root in roots)
            {
                if (color[root] != 0)
                {
                    continue;
                }
                var stack = new Stack<(string v, IEnumerator<TaggedEdge<string, (bool branch, StateUpdate stateUpdate)>> edges)>();
                color[root] = 1;
                stack.Push((root, this.G.OutEdges(root).GetEnumerator()));
                while (stack.Count > 0)
                {
                    (string v, IEnumerator<TaggedEdge<string, (bool branch, StateUpdate stateUpdate)>> it) = stack.Peek();
                    if (it.MoveNext())
                    {
                        string w = it.Current.Target;
                        switch (color[w])
                        {
                            case 0:
                                color[w] = 1;
                                stack.Push((w, this.G.OutEdges(w).GetEnumerator()));
                                break;
                            case 1: // w on the stack ⇒ back-edge
                                this.backEdges_.Add((v, w));
                                this.headers_.Add(w);
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        color[v] = 2;
                        postorder.Add(v);
                        stack.Pop();
                    }
                }
            }

            postorder.Reverse(); // reverse postorder = topological order over forward edges
            this.topo_.AddRange(postorder);
        }

        private bool IsBack(string src, string dst) => this.backEdges_.Contains((src, dst));
        #endregion

        #region Evaluation
        private void Evaluate()
        {
            foreach (string v in this.topo_)
            {
                this.before_[v] = this.headers_.Contains(v)
                    ? this.ResolveLoopHeader(v)
                    : this.ComputeForward(v, this.before_, planted: null);
            }
            foreach (string v in this.topo_)
            {
                this.after_[v] = this.ComputeAfter(v);
            }
        }

        /// <summary>after(v) = before(v) + the line's instruction (its out-edge effects, joined). For a
        /// terminal vertex after == before.</summary>
        private State ComputeAfter(string v)
        {
            string key = v + "!after";
            var outEdges = new List<TaggedEdge<string, (bool branch, StateUpdate stateUpdate)>>(this.G.OutEdges(v));
            if (outEdges.Count == 0)
            {
                return this.PlantAtKey(this.before_[v], key);
            }
            var contribs = new List<State>();
            foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in outEdges)
            {
                contribs.Add(this.ApplyEdge(this.before_[v], e));
            }
            State result = this.JoinContribsAt(contribs, key);
            foreach (State c in contribs)
            {
                c.Dispose();
            }
            return result;
        }

        /// <summary>
        /// before(v) = value-join of apply(known(src), edge) over the forward in-edges, planted clean at v.
        /// Provably-unreachable predecessors (inconsistent contributions) are dropped. Root ⇒ fresh unknown.
        /// <paramref name="planted"/>, when given, overrides the source state for specific keys (used by the
        /// loop-body propagation to pin the header).
        /// </summary>
        private State ComputeForward(string v, IReadOnlyDictionary<string, State> known, IReadOnlyDictionary<string, State>? planted)
        {
            var contribs = new List<State>();
            foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in this.G.InEdges(v))
            {
                if (this.IsBack(e.Source, e.Target))
                {
                    continue; // back-edge handled by the loop strategy
                }
                State? src = (planted != null && planted.TryGetValue(e.Source, out State? p)) ? p
                    : (known.TryGetValue(e.Source, out State? k) ? k : null);
                if (src != null)
                {
                    contribs.Add(this.ApplyEdge(src, e)); // head = v (canonical)
                }
            }

            State result = this.JoinContribsAt(contribs, v);
            foreach (State c in contribs)
            {
                c.Dispose();
            }
            return result;
        }

        private State ResolveLoopHeader(string header)
        {
            State entry = this.ComputeForward(header, this.before_, planted: null); // clean, planted at header
            HashSet<string> loopVerts = this.LoopVertices(header);
            ModSet modSet = this.WriteSetOf(loopVerts);

            bool nested = loopVerts.Any(w => w != header && this.headers_.Contains(w));
            this.LoopCount++;

            ILoopStrategy strat = nested ? new AcceptStrategy() : this.strategy_;
            if (nested)
            {
                AsmLog.Debug("SIM", "ComponentEvaluator: nested/irreducible loop at " + header + " ⇒ Accept fallback");
            }

            using State summary = strat.ResolveHeader(
                entry,
                s => this.OneIteration(header, s, loopVerts),
                modSet,
                this.tools_.LoopBudget);
            entry.Dispose();

            // Re-plant the summary at the vertex key with a clean solver (out-edges read reg@header).
            return this.PlantAtKey(summary, header);
        }

        /// <summary>Run the loop body once from a header state; return (clean, planted at a unique key) the
        /// join arriving back at the header along the back-edges — the <see cref="ILoopStrategy"/> body.</summary>
        private State OneIteration(string header, State headerState, HashSet<string> loopVerts)
        {
            this.LoopIterations++;
            using State pinnedHeader = this.PlantAtKey(headerState, header); // body edges read reg@header
            Dictionary<string, State> local = this.PropagateBody(header, pinnedHeader, loopVerts);

            var backContribs = new List<State>();
            foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in this.G.InEdges(header))
            {
                if (this.IsBack(e.Source, e.Target) && local.TryGetValue(e.Source, out State? src))
                {
                    backContribs.Add(this.ApplyEdge(src, e)); // head = header
                }
            }

            string uniq = header + "!iter" + (++this.keyCounter_);
            State result = backContribs.Count == 0
                ? this.PlantAtKey(pinnedHeader, uniq)
                : this.JoinContribsAt(backContribs, uniq);

            foreach (State c in backContribs)
            {
                c.Dispose();
            }
            foreach (State s in local.Values)
            {
                if (!ReferenceEquals(s, pinnedHeader))
                {
                    s.Dispose();
                }
            }
            return result;
        }

        /// <summary>Forward-propagate a pinned header through the loop body; each body vertex's state is
        /// clean and planted at its canonical key.</summary>
        private Dictionary<string, State> PropagateBody(string header, State pinnedHeader, HashSet<string> loopVerts)
        {
            var local = new Dictionary<string, State> { [header] = pinnedHeader };
            foreach (string v in this.topo_)
            {
                if (v == header || !loopVerts.Contains(v))
                {
                    continue;
                }
                local[v] = this.ComputeForward(v, this.before_, planted: local);
            }
            return local;
        }
        #endregion

        #region State helpers
        /// <summary>Copy a state and apply an edge's StateUpdate; the result's head is the edge target key.</summary>
        private State ApplyEdge(State src, TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> edge)
        {
            State c = new(src);
            c.Update_Forward(edge.Tag.stateUpdate);
            return c;
        }

        /// <summary>Value-join the (consistent) contributions per bit and plant the result clean at
        /// <paramref name="key"/>. Inconsistent contributions are dropped; if all are inconsistent the result
        /// is itself inconsistent (the vertex is unreachable). No contribution list ⇒ fresh unknown.</summary>
        private State JoinContribsAt(List<State> contribs, string key)
        {
            if (contribs.Count == 0)
            {
                return new State(this.flow_.FlowContext, this.tools_, key, key); // fresh unknown
            }

            var live = contribs.Where(c => c.IsConsistent != Tv.ZERO).ToList();
            if (live.Count == 0)
            {
                State dead = new(contribs[0]); // unreachable: keep an inconsistent state at this vertex
                dead.HeadKey = key;
                dead.TailKey = key;
                return dead;
            }

            var regJoin = new Dictionary<Rn, Tv[]>();
            foreach (Rn r in this.tools_.StateConfig.GetRegOn())
            {
                Tv[] acc = (Tv[])live[0].GetTvArray(r).Clone();
                for (int i = 1; i < live.Count; ++i)
                {
                    JoinInto(acc, live[i].GetTvArray(r));
                }
                regJoin[r] = Sanitize(acc);
            }
            var flagJoin = new Dictionary<Flags, Tv>();
            foreach (Flags f in this.tools_.StateConfig.GetFlagOn())
            {
                Tv acc = live[0].GetTv(f);
                for (int i = 1; i < live.Count; ++i)
                {
                    acc = JoinTv(acc, live[i].GetTv(f));
                }
                flagJoin[f] = Sanitize(acc);
            }
            return this.PlantValues(regJoin, flagJoin, key);
        }

        /// <summary>Rebuild a clean state at <paramref name="key"/> holding <paramref name="snapshot"/>'s
        /// per-reg/flag values (read at its head).</summary>
        private State PlantAtKey(State snapshot, string key)
        {
            var regJoin = new Dictionary<Rn, Tv[]>();
            foreach (Rn r in this.tools_.StateConfig.GetRegOn())
            {
                regJoin[r] = Sanitize((Tv[])snapshot.GetTvArray(r).Clone());
            }
            var flagJoin = new Dictionary<Flags, Tv>();
            foreach (Flags f in this.tools_.StateConfig.GetFlagOn())
            {
                flagJoin[f] = Sanitize(snapshot.GetTv(f));
            }
            return this.PlantValues(regJoin, flagJoin, key);
        }

        private State PlantValues(Dictionary<Rn, Tv[]> regs, Dictionary<Flags, Tv> flags, string key)
        {
            State result = new(this.flow_.FlowContext, this.tools_, key, key);
            using StateUpdate u = new(key + "!plant", key, this.tools_);
            foreach (KeyValuePair<Rn, Tv[]> kv in regs)
            {
                u.Set(kv.Key, kv.Value);
            }
            foreach (KeyValuePair<Flags, Tv> kv in flags)
            {
                u.Set(kv.Key, kv.Value);
            }
            result.Update_Forward(u);
            result.TailKey = key;
            return result;
        }

        private static void JoinInto(Tv[] acc, Tv[] other)
        {
            for (int i = 0; i < acc.Length && i < other.Length; ++i)
            {
                acc[i] = JoinTv(acc[i], other[i]);
            }
        }

        private static Tv JoinTv(Tv a, Tv b) => a == b ? a : Tv.UNKNOWN;

        private static Tv Sanitize(Tv t) => (t == Tv.ZERO || t == Tv.ONE || t == Tv.UNDEFINED) ? t : Tv.UNKNOWN;

        private static Tv[] Sanitize(Tv[] arr)
        {
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = Sanitize(arr[i]);
            }
            return arr;
        }
        #endregion

        #region Loop structure
        private HashSet<string> LoopVertices(string header)
        {
            var backSources = new List<string>();
            foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in this.G.InEdges(header))
            {
                if (this.IsBack(e.Source, e.Target))
                {
                    backSources.Add(e.Source);
                }
            }

            HashSet<string> fwd = this.ForwardReach(header);
            HashSet<string> bwd = this.BackwardReach(backSources);
            var loop = new HashSet<string> { header };
            foreach (string v in fwd)
            {
                if (bwd.Contains(v))
                {
                    loop.Add(v);
                }
            }
            return loop;
        }

        private HashSet<string> ForwardReach(string start)
        {
            var seen = new HashSet<string> { start };
            var q = new Queue<string>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in this.G.OutEdges(q.Dequeue()))
                {
                    if (!this.IsBack(e.Source, e.Target) && seen.Add(e.Target))
                    {
                        q.Enqueue(e.Target);
                    }
                }
            }
            return seen;
        }

        private HashSet<string> BackwardReach(IEnumerable<string> starts)
        {
            var seen = new HashSet<string>();
            var q = new Queue<string>();
            foreach (string s in starts)
            {
                if (seen.Add(s))
                {
                    q.Enqueue(s);
                }
            }
            while (q.Count > 0)
            {
                foreach (TaggedEdge<string, (bool branch, StateUpdate stateUpdate)> e in this.G.InEdges(q.Dequeue()))
                {
                    if (!this.IsBack(e.Source, e.Target) && seen.Add(e.Source))
                    {
                        q.Enqueue(e.Source);
                    }
                }
            }
            return seen;
        }

        private ModSet WriteSetOf(IEnumerable<string> verts)
        {
            var regs = new HashSet<Rn>();
            Flags flags = Flags.NONE;
            foreach (string v in verts)
            {
                int line = this.flow_.LineNumber(v);
                if (line < 0 || !this.sFlow_.HasLine(line))
                {
                    continue;
                }
                (Mnemonic mnemonic, string[] args) = this.sFlow_.Get_Line(line);
                if (mnemonic == Mnemonic.NONE)
                {
                    continue;
                }
                using OpcodeBase? op = Runner.InstantiateOpcode(mnemonic, args, ("d_p", "d_n", "d_b"), this.tools_);
                if (op == null)
                {
                    continue;
                }
                foreach (Rn r in op.RegsWriteStatic)
                {
                    regs.Add(RegisterTools.Get64BitsRegister(r));
                }
                flags |= op.FlagsWriteStatic;
            }
            return new ModSet(regs, flags);
        }
        #endregion

        public void Dispose()
        {
            foreach (State s in this.before_.Values)
            {
                s.Dispose();
            }
            foreach (State s in this.after_.Values)
            {
                s.Dispose();
            }
            this.before_.Clear();
            this.after_.Clear();
        }
    }
}
