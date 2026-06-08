# Invariant taxonomy (design checklist for fuzz invariants)

A vocabulary of **21 abstract invariant families** for designing fuzz invariants beyond "doesn't
crash". Tag each concrete invariant with its family so coverage gaps become visible ("we have CONS and
NEG but no IDEM — should we?") and so new targets can walk a checklist instead of starting blank.

> **Provenance:** adapted from the algebraic-invariant framework (§3.5) of the MinIO/eos-wt1
> "wide-fuzzer specification" (a stateful FSM/oracle fuzzer for the S3 object layer). Statements are
> paraphrased; the **asm-dude** column maps each family to *this* fuzzer (a mostly-pure parser + a
> stateful LSP server). Legend: ✅ good fit / cheap · ◐ partial / medium cost · ✗ no natural instance.

## Where asm-fuzz stands today
The targets now assert **eleven** families. Each is tagged in `Invariants.cs` / the target that calls it:

| Family | How it's asserted | Where |
|--------|-------------------|-------|
| **CONS** | token spans / LSP ranges / positions / folding / diagnostics / label-graph defs in-bounds | `parseline`, `splitintokeywords`, `documentpipeline`, all LSP targets, `labelgraph` |
| **MONO** | keyword spans ordered & non-overlapping; semantic-token delta array sorted | `splitintokeywords`, `documentpipeline` |
| **IDEM** | two fresh servers on identical text → identical semantic tokens | `documentpipeline` |
| **DUAL** | `close ∘ open` restores baseline (`TrackedDocumentEntryCount == 0`) | every server target |
| **HOM** | `ParseLine` classification is independent of the `lineNumber`/`fileID` args | `parseline` |
| **COMM** | opening a second document on a distinct URI doesn't perturb the first | `multidocisolation` |
| **BND** | `Global_MaxFileLines` cap honored; edits don't grow the per-doc footprint | `labelgraph`, `documentchange` |
| **RES** | reaching content X by edit ≡ a fresh open of X (sync edit seam) | `documentchange` |
| **NEG** | malformed settings JSON → only `JsonException`; predicate↔parser classifier agreement (`IsRn`↔`ParseRn`, `IsMnemonic`↔`ParseMnemonic`, `ToRn`↔`ParseRn`) | `settings`, `classifyconsistency` |
| **TERM** | bounded time | libFuzzer / campaign timeout |

Remaining headroom (uncovered, by design cost): **ID** (full-line reconstruct round-trip), **INJ**
(distinct register/mnemonic spellings → distinct enum values — hard to check from outside the dict-built
cache), **ORACLE** (reference tokenizer), **SYM** (metamorphic relabeling). The rest are N/A (see table).

> **Note on `NBits`:** an early version of `classifyconsistency` also asserted "a valid register has a
> positive bit-width" — the fuzzer immediately produced `gs` (and by extension the segment / mask /
> control / debug / bound register classes), which `RegisterTools.NBits` intentionally sizes to 0 (it is
> a *data*-register width helper, not a total function over `Rn`). That clause was a false invariant and
> was removed; it is recorded here so it isn't re-added.

## State-shape laws — what state can exist
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **CONS** | Conservation / no-loss | Tracked entities persist unless an explicit delete removed them; nothing silently disappears. | ✅ Token offsets/ranges from `ParseLine`/`SplitIntoKeywords` are in-bounds (`0≤begin<end≤len`); every `LabelGraph` ref/diagnostic points to a real line/token; no token silently dropped. |
| **ID** | Identity preservation | Identity-bearing fields (hash, size, type) survive move/copy/migrate. | ◐ Roundtrip: `parse → reconstruct → parse` preserves `(mnemonic, args, label, token types)`. |
| **BND** | Bounded resources | Caps (caches, queues, chains) are honored and audited at quiescence. | ◐ `Global_MaxFileLines` honored; LSP server dictionaries/caches don't grow unboundedly across edits. |

## Transition laws — how state changes
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **MONO** | Monotonicity | Designated "progress" quantities only move forward. | ✅ Semantic-token positions are strictly increasing & non-overlapping (LSP delta-encoding requires it — a violation silently corrupts highlighting and never throws); token begins monotonic; doc `version` monotonic. |
| **IDEM** | Idempotence (`f∘f = f`) | Repeating a terminal op gives the same result. | ◐ Re-opening identical content yields identical `parsedDocuments`; any normalizer satisfies `f(f(x))=f(x)`. |
| **ATOM** | Atomicity | Multi-step ops are wholly observable or not at all; no partial commits. | ✗ Mostly N/A — partial: a failed `OnTextDocumentOpened` must not leave half-populated dictionaries. |
| **DUAL** | Duality / inverse ops | Paired ops (PUT/DELETE, Initiate/Abort) cancel to baseline or are mutually exclusive. | ✅ (LSP) `close ∘ open` returns server state (`textDocuments`/`parsedDocuments`/`labelGraphs`/caches) to baseline — catches state leaks (relevant to per-input reproducibility). |
| **INVOL** | Involution / self-inverse (`f∘f = id`) | One op is its own inverse; twice = original. Catches ghost-state leaks. | ◐ Mostly N/A for a parser; would apply to any toggle-style transform. |
| **REV** | Reversibility / antiautomorphism | A multi-step transaction aborting at step k rolls back in **reverse** order. | ✗ N/A — no multi-step transactions. |
| **ASSOC** | Associativity (`f(f(x,y),z)=f(x,f(y,z))`) | Bracketing of a binary op doesn't matter. | ✗ N/A. |
| **HOM** | Homomorphism (`f(g(x,y))=g(f(x),f(y))`) | `f` distributes over a structure `g`. | ◐ Line independence: tokenizing `A\nB` equals per-line tokens of `A` and `B` concatenated — catches cross-line state bleed. |

## Function-shape laws — properties of individual operations
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **INJ** | Injectivity (`f(x)=f(y) ⇒ x=y`) | Distinct inputs → distinct outputs; ID-generation has no collisions. | ◐ Distinct mnemonic spellings map to distinct `Mnemonic` values (no accidental enum collision); classification mapping injective where intended. |
| **PERM** | Permutation invariance | For collection args, output depends on the **set**, not the order. | ✗ N/A — line parsing is order-dependent (pin this as a *non*-PERM property if ever relevant). |

## Concurrency laws — multi-actor / multi-step
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **COMM** | Commutativity on disjoint | Ops on disjoint resources commute; final state is order-independent. | ◐ (LSP) Ops on distinct document URIs don't interfere — underpins per-input isolation. |
| **LIN** | Linearizability under contention | Ops on shared resources serialize to some valid order. | ✗ N/A — single-threaded. |
| **QUO** | Quorum / consistency floor | Observable state never violates the consistency floor; cross-check via an **independent** code path. | ✗ N/A as quorum — but the *independent-path* idea is gold: cross-check two implementations that should agree (see §"Independence" below). |

## Failure laws — when things go wrong
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **NEG** | Negative-outcome conformance | Bad input yields the *documented* error — not success, not crash, not a different error. | ✅ Invalid token is never classified as a valid `Mnemonic`/`Register`; malformed settings JSON throws *only* `JsonException` (the `settings` target already does this — generalise it). |
| **RES** | Resumability / replay equivalence | `crash; recover; continue ≡ continue` for any committed prefix. | ◐ **Incremental == full**: after an edit, server state equals opening the new content fresh (directly relevant to incremental-sim work). |
| **TERM** | Termination | Every started op finishes in bounded time. | ✅ Covered by the libFuzzer/campaign timeout (the only family asserted today). |

## Meta-laws — fuzzer ↔ system contract
| ID | Family | Statement | asm-dude |
|----|--------|-----------|----------|
| **ORACLE** | Oracle = system at quiescence | A ground-truth model agrees with observed state after the run. | ◐ Biggest investment: a reference tokenizer/model to diff parser output against (turns "valid" into "correct"). |
| **SYM** | Symmetry / metamorphic | Permuting interchangeable labels yields an isomorphic state up to the same relabeling. | ◐ Consistently renaming labels/registers produces isomorphic token structure (metamorphic test). |

## Two meta-practices worth adopting (independent of the families)
- **Family-tag every invariant.** Tagging makes coverage gaps visible at a glance and turns invariant
  design into a checklist walk rather than a blank-page exercise.
- **Independence / blind-spot tagging.** A check that shares a code path with the thing it validates is
  weak (theirs: `L-1` uses `ListObjectsV2`, so the genuinely independent `L-INDEP` uses `WalkDir`
  instead). Same principle as this repo's CLAUDE.md rule "a test must be able to fail": prefer
  cross-checks between *independent* implementations (e.g. parser classification vs. an independent
  re-derivation), and explicitly note when two checks are NOT independent.
