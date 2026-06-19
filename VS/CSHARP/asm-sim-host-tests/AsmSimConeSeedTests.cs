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

namespace AsmSim.Host.Tests;

using System.Collections.Generic;

using AsmSim;

using FluentAssertions;

using Xunit;

/// <summary>
/// Guards the "cone lenses don't collapse on edit" fix. The reuse base for a cone re-solve must SEED the cone
/// lines (the lines being re-solved) with their previous value — not omit them — so each lens holds its
/// vertical space during the ~1-2 s re-solve and is overwritten in place, instead of vanishing (which reclaims
/// the row and makes the code jump). Verified directly on <see cref="AsmSimulator.RemapConeReuse"/>.
/// </summary>
public class AsmSimConeSeedTests
{
    [Fact]
    public void RemapConeReuse_SeedsMatchedConeLines_SoDownstreamLensesDoNotCollapse()
    {
        // Baseline cache: every instruction line has a proven write label.
        var prev = new AsmSimulator.DocCache();
        prev.lineStringsWriteLabels[0] = "→RAX=0x1";
        prev.lineStringsWriteLabels[1] = "→RBX=0x2";
        prev.lineStringsWriteLabels[2] = "→RAX=0x3";

        // Edit line 1's operand (rbx 2→3). Lines 0 and 2 still match (NewToOld); line 1 is the changed line.
        var oldInstr = Instruction.ParseProgram(["mov rax, 1", "mov rbx, 2", "add rax, rbx"]);
        var newInstr = Instruction.ParseProgram(["mov rax, 1", "mov rbx, 3", "add rax, rbx"]);
        InstructionDiff diff = InstructionDiff.Compute(oldInstr, newInstr);

        // Cone = the edited line plus its downstream dependent (line 2 reads rbx).
        var cone = new HashSet<int> { 1, 2 };

        AsmSimulator.DocCache reuse = AsmSimulator.RemapConeReuse(prev, diff, cone);

        // The non-cone line is reused (unchanged behavior).
        reuse.lineStringsWriteLabels.Should().ContainKey(0, "the untouched line keeps its label");
        // THE FIX: the matched downstream cone line (2) is SEEDED with its old value so its lens stays put
        // until the engine overwrites it — it must not be omitted (which collapsed the lens before).
        reuse.lineStringsWriteLabels.Should().ContainKey(2,
            "the downstream cone line must be seeded with its previous value, not dropped");
        reuse.lineStringsWriteLabels[2].Should().Be("→RAX=0x3");
    }
}
