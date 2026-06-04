// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS.Tests;

using System.Collections.Generic;

using AsmSourceTools;

using AsmTools;

using FluentAssertions;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using Xunit;

/// <summary>
/// Locks the DNF (disjunctive normal form) architecture semantics on <see cref="AsmSignatureInformation"/>.
/// The VADDPS XMM form requires "(AVX512_VL AND AVX512_F) OR AVX10" — enabling only one of VL/F must
/// NOT make it available; enabling both, or AVX10 alone, must. These tests fail against the old flat-OR
/// model (which treated the arch column as a plain OR and would accept AVX512_F alone).
/// </summary>
public class ArchDnfTests
{
    // Minimal AsmSignatureInformation carrying just the arch requirement under test.
    private static AsmSignatureInformation Make(Arch[][] dnf) => new()
    {
        Mnemonic = Mnemonic.VADDPS,
        Arch = dnf,
        Operands = new List<IList<AsmSignatureEnum>>(),
        SignatureInformation = new SignatureInformation { Label = "VADDPS" },
    };

    private static HashSet<Arch> On(params Arch[] archs) => [.. archs];

    [Fact]
    public void Is_Allowed_AndGroup_RequiresAllMembers()
    {
        // (AVX512_VL AND AVX512_F) OR AVX10
        var sig = Make([[Arch.ARCH_AVX512_VL, Arch.ARCH_AVX512_F], [Arch.ARCH_AVX10]]);

        sig.Is_Allowed(On(Arch.ARCH_AVX512_F)).Should().BeFalse("F alone does not satisfy (VL AND F)");
        sig.Is_Allowed(On(Arch.ARCH_AVX512_VL)).Should().BeFalse("VL alone does not satisfy (VL AND F)");
        sig.Is_Allowed(On(Arch.ARCH_AVX512_VL, Arch.ARCH_AVX512_F)).Should().BeTrue("VL AND F satisfies the first group");
        sig.Is_Allowed(On(Arch.ARCH_AVX10)).Should().BeTrue("AVX10 satisfies the second group");
        sig.Is_Allowed(On(Arch.ARCH_AVX2)).Should().BeFalse("an unrelated arch satisfies no group");
    }

    [Fact]
    public void Is_Allowed_EmptyRequirement_IsAlwaysAllowed()
    {
        // No architecture gate (e.g. a base/legacy instruction) => always offered.
        var sig = Make([]);
        sig.Is_Allowed(On()).Should().BeTrue();
        sig.Is_Allowed(On(Arch.ARCH_8086)).Should().BeTrue();
    }

    [Fact]
    public void Is_Allowed_SingletonGroups_BehaveAsPlainOr()
    {
        // Backward compatibility: a flat OR list (each arch its own singleton group) keeps OR semantics.
        var sig = Make([[Arch.ARCH_AVX], [Arch.ARCH_AVX2]]);
        sig.Is_Allowed(On(Arch.ARCH_AVX2)).Should().BeTrue();
        sig.Is_Allowed(On(Arch.ARCH_AVX)).Should().BeTrue();
        sig.Is_Allowed(On(Arch.ARCH_SSE)).Should().BeFalse();
    }
}
