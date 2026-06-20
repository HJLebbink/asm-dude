// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2.Vsix.Tests;

using System.Collections.Generic;
using System.Linq;

using AsmDude2;

using Xunit;

/// <summary>
/// Tests the VS-free CodeLens tag-building core (<see cref="AsmCodeLensTagBuilder"/>) the tagger uses to turn
/// server sim-state/label data into per-line lenses + a dedup signature.
///
/// <para>The guarded invariant is the one whose absence caused the live bug ("edited asm, CodeLens kept showing
/// stale values"): a CHANGE to a line's sim value MUST change the content signature, because the tagger skips a
/// republish when the signature is unchanged. If the signature did not move on a value change, the stale lens
/// would never be re-published. This is the CodeLens-layer counterpart to the sim-layer cone tests.</para>
/// </summary>
public class AsmCodeLensTagBuilderTests
{
    // 3-line straight program; offsets are arbitrary-but-consistent line starts (20 chars apart).
    private static List<CodeLensLineInfo> ThreeLines() =>
    [
        new(0, 0, "mov rax, 0x10"),
        new(1, 20, "mov rdx, rax"),
        new(2, 40, "ret"),
    ];

    private static string SignatureFor(IReadOnlyDictionary<int, string> simStates)
    {
        (_, string sig) = AsmCodeLensTagBuilder.Build(ThreeLines(), simStates, []);
        return sig;
    }

    [Fact]
    public void DownstreamValueChange_ChangesSignature_SoTheLensIsRepublished()
    {
        // Editing line 0 (mov rax) changes line 1's proven value: rdx 0x10 → 0x40. The downstream line's
        // text is identical; only its sim VALUE changed. The signature must still differ.
        string before = SignatureFor(new Dictionary<int, string> { [1] = "→RDX=0x10" });
        string after = SignatureFor(new Dictionary<int, string> { [1] = "→RDX=0x40" });

        Assert.NotEqual(before, after); // else the stale lens is dedup-skipped
    }

    [Fact]
    public void IdenticalSimStates_ProduceIdenticalSignature_SoNoRedundantRepublish()
    {
        var states = new Dictionary<int, string> { [0] = "→RAX=0x10", [1] = "→RDX=0x10" };

        // Unchanged data must dedup (identical signature), otherwise the lenses flicker.
        Assert.Equal(SignatureFor(states), SignatureFor(new Dictionary<int, string>(states)));
    }

    [Fact]
    public void SimStateTag_CarriesTheValueInItsDescription()
    {
        (List<CodeLensTagSpec> specs, _) = AsmCodeLensTagBuilder.Build(
            ThreeLines(), new Dictionary<int, string> { [1] = "→RDX=0x40" }, []);

        CodeLensTagSpec sim = specs.Single(s => s.Kind == AsmCodeLensTagKind.SimState);
        Assert.Equal(1, sim.LineNumber);
        // The renderer (AsmSimStateCodeLens) unpacks the value from this description.
        Assert.Equal("simstate:|→RDX=0x40", sim.Description);
    }

    [Fact]
    public void EmptySimValue_CarriesNoTag_ButBlankLineWithAValueDoes()
    {
        // A sim label at a blank line cannot be rendered there (VS shifts the layout), so it floats to the
        // next non-empty line. An empty value produces no tag at all.
        List<CodeLensLineInfo> lines = [new(0, 0, "mov rax, 1"), new(1, 11, ""), new(2, 12, "ret")];
        var states = new Dictionary<int, string> { [0] = "", [1] = "→RAX=1" };

        (List<CodeLensTagSpec> specs, _) = AsmCodeLensTagBuilder.Build(lines, states, []);

        Assert.DoesNotContain(specs, s => s.LineNumber == 0); // empty value ⇒ no lens
        Assert.DoesNotContain(specs, s => s.LineNumber == 1); // never on the blank line itself
        Assert.Contains(specs, s => s.LineNumber == 2 && s.Kind == AsmCodeLensTagKind.SimState); // floated to next line
    }

    [Fact]
    public void WriteBelowAnInstruction_OverABlankLine_FloatsToTheNextNonEmptyLine()
    {
        // The reported scenario: 'mov rbx, 10', a BLANK line (just typed — note VS AUTO-INDENTS it with
        // whitespace, so it is NOT length 0), then 'add rax, rbx'. The mov's after-state is at display
        // position 1 (the whitespace line). It must float to line 2 (the add) — exactly where it sat before
        // the blank existed — not vanish and not render on the whitespace line.
        List<CodeLensLineInfo> lines = [new(0, 0, "mov rbx, 10"), new(1, 12, "    "), new(2, 17, "add rax, rbx")];
        var states = new Dictionary<int, string> { [1] = "w:RBX=0xA" };

        (List<CodeLensTagSpec> specs, _) = AsmCodeLensTagBuilder.Build(lines, states, []);

        CodeLensTagSpec sim = specs.Single(s => s.Kind == AsmCodeLensTagKind.SimState);
        Assert.Equal(2, sim.LineNumber); // floated down to the next non-empty line, never the blank line
    }

    [Fact]
    public void RelocatedWrite_CombinesWithTheNextLinesReads_RegisterShownOnce()
    {
        // mov rbx's write (over a whitespace-indented blank line) lands on the add line, which reads rbx. The
        // shared register collapses to rw: (shown once), not duplicated as both r:RBX and w:RBX.
        List<CodeLensLineInfo> lines = [new(0, 0, "mov rbx, 10"), new(1, 12, "\t"), new(2, 13, "add rax, rbx")];
        var states = new Dictionary<int, string>
        {
            [1] = "w:RBX=0xA",            // mov rbx's write, displayed below it (on the blank line)
            [2] = "r:RAX=0x6, r:RBX=0xA", // add's reads, displayed above it
        };

        (List<CodeLensTagSpec> specs, _) = AsmCodeLensTagBuilder.Build(lines, states, []);

        CodeLensTagSpec sim = specs.Single(s => s.Kind == AsmCodeLensTagKind.SimState && s.LineNumber == 2);
        Assert.Contains("rw:RBX=0xA", sim.Description); // read+write of RBX merged to rw:
        Assert.DoesNotContain("w:RBX", sim.Description.Replace("rw:RBX", "")); // not also a separate w:RBX
        Assert.Contains("r:RAX=0x6", sim.Description); // read-only RAX stays r:
    }

    [Fact]
    public void CombineLabels_PromotesSharedNameToReadWrite_AndPrefersConcreteValue()
    {
        // Read-only and write-only items pass through; a name on both sides becomes rw: with the concrete value.
        Assert.Equal("rw:RBX=0xA", AsmCodeLensTagBuilder.CombineLabels("r:RBX=0x?", "w:RBX=0xA"));
        Assert.Equal("r:RAX=0x6, w:RCX=0x7", AsmCodeLensTagBuilder.CombineLabels("r:RAX=0x6", "w:RCX=0x7"));
    }

    [Fact]
    public void LabelTag_RendersReferenceCount_AndCountsAsATaggedLine()
    {
        List<CodeLensLineInfo> lines = [new(0, 0, "loop_start:"), new(1, 20, "jne loop_start")];
        IReadOnlyList<AsmLabelRef> labels = [new("loop_start", DefinitionLine: 0, DefinitionColumn: 0, DefinitionLength: 10, ReferenceCount: 1)];

        (List<CodeLensTagSpec> specs, _) = AsmCodeLensTagBuilder.Build(lines, new Dictionary<int, string>(), labels);

        CodeLensTagSpec label = specs.Single(s => s.Kind == AsmCodeLensTagKind.Label);
        Assert.Equal("refcount:1|Label: loop_start", label.Description);
        Assert.Equal(1, AsmCodeLensTagBuilder.CountTaggedLines(specs));
    }
}
