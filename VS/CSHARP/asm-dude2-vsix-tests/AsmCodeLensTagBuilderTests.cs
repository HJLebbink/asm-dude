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
    public void EmptyLines_AndEmptySimValues_CarryNoTag()
    {
        List<CodeLensLineInfo> lines = [new(0, 0, ""), new(1, 5, "mov rax, 1")];
        var states = new Dictionary<int, string> { [0] = "ignored (blank line)", [1] = "" }; // empty value ⇒ no tag

        (List<CodeLensTagSpec> specs, string sig) = AsmCodeLensTagBuilder.Build(lines, states, []);

        Assert.Empty(specs); // a blank line and an empty sim value produce no lens
        Assert.Equal(string.Empty, sig);
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
