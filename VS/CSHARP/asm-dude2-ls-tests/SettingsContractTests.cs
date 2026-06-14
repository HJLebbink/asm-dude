// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS.Tests;

using AsmTools;

using FluentAssertions;

using System.Text.Json;

using Xunit;

/// <summary>
/// Locks the settings contract shared between the VSIX (which serializes <see cref="AsmSettingsData"/>)
/// and the server (which deserializes <see cref="AsmLanguageServerOptions"/>, derived from it).
/// These exercise the real shared types + <see cref="ColorJsonConverter"/>, so they fail if the
/// inheritance split, field serialization, or the converter regresses.
/// </summary>
public class SettingsContractTests
{
    // Mirrors the options used by SettingsSyncService (producer) and SettingsManager (consumer):
    // fields included, case-insensitive, shared Color converter.
    private static JsonSerializerOptions MakeOptions() => new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new ColorJsonConverter() },
    };

    // ── AsmSim_Incremental default resolution (regression: an OLD settings.json silently disabled the
    // feature because the absent bool field deserialized to false). These drive the REAL production
    // deserializer (SettingsManager.DeserializeSettings) + the single-source effective resolver. ──

    [Fact]
    public void AsmSimIncremental_AbsentInOldSettings_DefaultsToEnabled()
    {
        // An older settings.json (written before the field existed) lacks AsmSim_Incremental. It MUST resolve
        // to the default (ON), NOT a bool's false — otherwise incremental simulation silently never runs for
        // existing users. (This is exactly the live-VS regression on 2026-06-14.)
        AsmLanguageServerOptions? opts = AsmDude2LS.SettingsManager.DeserializeSettings("{ \"AsmSim_On\": false }");

        opts.Should().NotBeNull();
        opts!.AsmSim_Incremental.Should().BeNull("an absent field is null, not false");
        opts.AsmSimIncrementalEffective.Should().BeTrue("absent must mean the default (ON)");
    }

    [Fact]
    public void AsmSimIncremental_ExplicitFalse_IsRespected()
    {
        AsmLanguageServerOptions? opts = AsmDude2LS.SettingsManager.DeserializeSettings("{ \"AsmSim_Incremental\": false }");
        opts!.AsmSim_Incremental.Should().BeFalse();
        opts.AsmSimIncrementalEffective.Should().BeFalse("an explicit opt-out must be honored");
    }

    [Fact]
    public void AsmSimIncremental_ExplicitTrue_IsRespected()
    {
        AsmLanguageServerOptions? opts = AsmDude2LS.SettingsManager.DeserializeSettings("{ \"AsmSim_Incremental\": true }");
        opts!.AsmSimIncrementalEffective.Should().BeTrue();
    }

    [Fact]
    public void Settings_RoundTrip_ProducerToConsumer_PreservesValues()
    {
        // Producer (VSIX) writes the base data type; consumer (server) reads the derived type.
        var produced = new AsmSettingsData
        {
            Global_MaxFileLines = 12345,
            AsmDoc_Url = "https://example.test/wiki/",
            AsmDoc_On = true,
            ARCH_AVX512_F = true,
            ARCH_X64 = true,
            AsmSim_Z3_Timeout_MS = 7777,
            CodeFolding_BeginTag = "#begin",
            SyntaxHighlighting_Opcode = System.Drawing.Color.FromArgb(255, 10, 20, 30),
        };

        string json = JsonSerializer.Serialize(produced, MakeOptions());
        var consumed = JsonSerializer.Deserialize<AsmLanguageServerOptions>(json, MakeOptions());

        consumed.Should().NotBeNull();
        consumed!.Global_MaxFileLines.Should().Be(12345);
        consumed.AsmDoc_Url.Should().Be("https://example.test/wiki/");
        consumed.AsmDoc_On.Should().BeTrue();
        consumed.ARCH_AVX512_F.Should().BeTrue();
        consumed.ARCH_X64.Should().BeTrue();
        consumed.AsmSim_Z3_Timeout_MS.Should().Be(7777);
        consumed.CodeFolding_BeginTag.Should().Be("#begin");
        consumed.SyntaxHighlighting_Opcode.R.Should().Be(10, "Color fields must round-trip via the shared ColorJsonConverter");
        consumed.SyntaxHighlighting_Opcode.G.Should().Be(20);
        consumed.SyntaxHighlighting_Opcode.B.Should().Be(30);
    }

    [Fact]
    public void Settings_DeserializedIntoDerivedType_BehaviorReadsInheritedFields()
    {
        // Verifies the inheritance split: AsmLanguageServerOptions' arch logic reads fields that now
        // live in the AsmSettingsData base, after a JSON round-trip through the contract.
        // Custom profile: the per-arch ARCH_* toggles drive the result (a non-Custom profile would
        // override them — covered by Test_ArchProfile in asm-tools-tests).
        var produced = new AsmSettingsData { ArchProfile = ArchProfileKeys.Custom, ARCH_X64 = true, ARCH_AVX2 = true };
        string json = JsonSerializer.Serialize(produced, MakeOptions());
        var consumed = JsonSerializer.Deserialize<AsmLanguageServerOptions>(json, MakeOptions())!;

        var arches = consumed.Get_Arch_Switched_On();
        arches.Should().Contain(Arch.ARCH_X64);
        arches.Should().Contain(Arch.ARCH_AVX2);
        arches.Should().NotContain(Arch.ARCH_AVX512_F, "an unset arch field must not be reported as enabled");
    }
}
