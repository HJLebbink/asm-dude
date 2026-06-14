# Research notes — strengthening the incremental cones for loop semantics

> **Status:** exploratory design notes, not a committed plan (2026-06-14). Captures a discussion about
> whether ideas from **cones-and-foci** (process-algebra verification) and from **Z3's fixed-point / CHC
> research** could improve the incremental-simulation cones (`DataflowCone` / `DynamicCone`, see
> `INCREMENTAL_SIM_PLAN.md`), especially around loops. Citations are from memory — **verify author/venue/year
> before relying on them** (flagged ⚠).

## 0. Two DISTINCT loop problems (don't conflate them)

The phrase "strengthen the cones for loop semantics" hides two separate issues:

1. **Cone *computation* over loops** — given an edit in/around a loop, which lines must be re-solved.
   We already do a **monotone worklist-to-fixpoint** (gen/kill, union meet) over the CFG *including*
   back-edges (`DynamicCone.Compute`). This is classical Kildall dataflow — correct and terminating.
   **Nothing below is needed for correctness here.**
2. **Simulation *precision* over loops** — the actual register/flag *values* across iterations (loop
   invariants). This is where the engine is weak: `DynamicFlow` is a **single-pass merge, not a fixpoint**,
   so a loop head reads its merged value as UNKNOWN (`mov rax,7; jmp start` ⇒ `rax` UNKNOWN). Knob:
   `LoopHandling` (`accept`|`modsethavoc`|`peelonce`|`fullunroll`|`fixpoint`), default `accept` (least precise).

The interesting cross-over ideas below mostly target **(2)**, and then feed back into **(1)** to make reuse
*tighter* (not more correct) around loops.

## 1. What transfers from cones-and-foci (Fokkink, Pang, Groote)

**Cones-and-foci** is a proof method in process algebra (μCRL/mCRL2): prove a linear implementation (with
hidden τ-actions) is **branching bisimilar** to a specification, via a state mapping `h`, **focus points**
(states with no internal progress — the implementation has "converged"), **cones** (states that reach a
focus by τ-steps), and a fixed set of **local matching criteria** (per-summand proof obligations) that imply
the global bisimulation. Original: Groote & Springintveld (required τ-*convergence*, no τ-loops ⚠~1995/2001).
Revisited: **Fokkink & Pang (with Groote)** *dropped the convergence requirement* (handles τ-cycles) and used
it to verify real protocols, e.g. the **Sliding Window Protocol** (⚠ early–mid 2000s, TCS/CONCUR).

Transferable ideas:

- **🥇 Focus point ≈ loop fixpoint, used as a CONE BOUNDARY.** A loop's stabilized fixpoint state is its
  "focus"; the iterations are the τ-steps that converge to it. Incremental payoff: **if an edit does not
  perturb the loop's focus (its loop-carried invariants), the loop body's displayed values are unchanged, so
  the cone can STOP at the loop** instead of conservatively re-solving everything reachable through the
  back-edge. Our dirty-set already reuses a body line that reads only *clean* registers (and the back-edge
  iteration converges), but it does **NOT** yet handle **loop-carried** dependencies (a value depending on a
  register the loop rewrites each iteration). A focus/invariant condition is exactly the tool for those.
- **🥈 Local matching criteria → local soundness instead of a global oracle.** Today reuse correctness is a
  *differential test* (`SimResultComparer.Compare(incremental, full).IsEmpty`). The cones-and-foci philosophy
  — discharge fixed *local* obligations and get the global equivalence for free — suggests **proving**
  `incremental == full` from local conditions on the cone boundary (each reused line's before-state matches;
  at the focus the focus condition holds) rather than testing it.
- τ-loop insensitivity (the Fokkink–Pang relaxation): the precedent for reasoning about equivalence even with
  internal cycles — i.e. reusing loop-body results across edits that don't perturb the loop, despite the loop
  "running forever."

**Honest mismatch (so we borrow structure, not theorems):**
- **Different equivalence.** Cones-and-foci proves *branching bisimulation* over an action-labelled LTS. Our
  correctness is *equality of concrete per-line register/flag values* — a data-level equivalence, not a
  behavioural one. The matching criteria are tailored to τ/action bisimulation; they do not hand us
  value-equality lemmas directly.
- **No natural spec/impl/LPE framing.** The only "specification" our incremental sim must equal is the full
  sim, and incremental-vs-full is value equality, not bisimulation. Instantiating the full μCRL apparatus
  would be forcing it. Take the *geometry* (cones → focus → local conditions), not the proof machinery.

## 2. Z3 / SMT research on loop semantics (how to actually GET the focus)

Cones-and-foci *verifies* a given mapping; it does **not** synthesize a loop invariant. For that — i.e. to
compute the focus (the loop-head fixpoint state) — the relevant theory is invariant inference. Z3 is a
decision procedure with no notion of "loop"; loop reasoning is built on top, and the most literal "Z3 research
on loops" is its **fixed-point / Constrained-Horn-Clause engine**:

- **μZ** — Z3's `Fixedpoint` module (Datalog + PDR). ⚠ *Hoder, Bjørner, de Moura, "μZ — An Efficient Engine
  for Fixed Points with Constraints," CAV 2011.*
- **GPDR** — lifts Bradley's **IC3/PDR** (hardware model checking) to SMT/arithmetic. ⚠ *Hoder & Bjørner,
  "Generalized Property Directed Reachability," SAT 2012.*
- **Spacer** — the CHC engine now default *inside* Z3; the workhorse. ⚠ *Komuravelli, Gurfinkel, Chaki,
  "SMT-Based Model Checking for Recursive Programs," CAV 2014.*

The framing is **Constrained Horn Clauses (CHC)**: encode the loop with an unknown predicate `Inv(state)` at
the loop head; clauses assert `Inv` is initiation-implied, **inductive** across the body, and implies the
post-condition. **Solving the CHC system = synthesizing the inductive loop invariant.** Z3 exposes this via
the `HORN` logic / `Fixedpoint` API; front-ends **SeaHorn / Korn / JayHorn** compile C/LLVM/Java loops to
CHCs and let Spacer solve them.

Adjacent invariant-inference techniques that ride on Z3:
- **Houdini** (guess a candidate-invariant pool, use the solver to filter to the inductive subset). ⚠ *Flanagan & Leino.*
- **Abstract interpretation + SMT** — fixpoint in an abstract domain (intervals/octagons/polyhedra) with
  *widening/narrowing* (⚠ *Cousot & Cousot*), SMT for the checks. Cheap, terminating — the usual choice for
  *interactive* tools.
- **Craig interpolation** (⚠ *McMillan*; iZ3 historically in Z3) — invariants from interpolants of bounded-unrolling traces.
- **MBQI** — model-based quantifier instantiation (⚠ *de Moura & Bjørner*) — for **quantified** invariants
  over arrays/memory (`∀i. …`). Directly relevant to our **memory** case, which the dynamic cone currently
  treats fully conservatively.

## 3. The composition (the actual idea worth trying)

**Spacer's (or AI's) inductive invariant IS the loop's focus point.**
- Abstract interpretation (cheap, interactive) or Spacer/CHC (precise, offline/on-demand) **computes the
  focus** — the stabilized loop-head state.
- The cones idea then **exploits the focus as the incremental cone's loop boundary**: *an edit that does not
  change the loop's invariant does not propagate into the body.*

So the two research lines are **complementary, not competing**: CHC/PDR (or AI) on the precision side
(problem 2), cones-and-foci-style reasoning on the incremental-reuse side (problem 1).

Engineering caveat: a Spacer call per loop can be slow / non-terminating — fine for an explicit "verify"
action, risky at keystroke latency. So: cheap interval/octagon AI as the default loop focus; Spacer reserved
for on-demand precision.

## 4. Smallest falsifiable next experiment

1. Make `LoopHandling=fixpoint` **expose the loop-head invariant state** (the focus) it computes.
2. Add ONE cone rule (in `DynamicCone`/`DataflowCone`): *an edit whose dirty-set leaves the loop-head focus
   state unchanged does not propagate into the loop body* — i.e. the loop becomes a cone boundary, its body
   lines reusable, instead of being swept in by back-edge reachability.
3. Validate against the existing differential oracle (`SimResultComparer`) on loop programs with loop-carried
   dependencies — exactly the case the current dirty-set over-includes.

If the oracle stays clean while the cone shrinks on loop edits, the focus-boundary idea pays off. If not, the
diff shows precisely which loop-carried value the focus abstraction missed.

## 5. Open questions
- Can a *value-level* analogue of the matching criteria be stated and discharged locally (proving
  `incremental == full` without running the full sim), or is the differential oracle the pragmatic ceiling?
- Memory/quantified invariants (MBQI) to lift the conservative "any memory write dirties all later reads"
  rule — is it worth it for editor-latency, or only offline?
- Which abstract domain (intervals vs octagons) gives a useful loop focus cheaply enough for interactive use?

See also: `INCREMENTAL_SIM_PLAN.md` (the cones we'd strengthen), `DynamicCone.cs` / `DataflowCone.cs`.
