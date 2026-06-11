// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmTools;

/// <summary>
/// Canonical string keys for the <c>archProfile</c> instruction-set-profile dropdown. A profile is a
/// one-click preset that selects a whole family of architectures, so users do not have to toggle the
/// ~100 individual CPUID feature flags by hand. Shared between the VSIX setting definition
/// (<c>ArchitectureSettings.ArchProfile</c>'s enum entries + <c>SettingsSyncService</c>) and the server
/// (<c>ArchTools.TryGetProfileArchs</c>, which expands a key into the concrete <c>Arch</c> set), so both
/// sides reference the same symbol — a rename is a compile error, not a silent string mismatch.
///
/// <para><b>Semantics:</b> when the profile is anything other than <see cref="Custom"/>, it OVERRIDES the
/// individual <c>ARCH_*</c> toggles; <see cref="Custom"/> means "honor the detailed toggles" (the
/// historical behavior). The v1–v4 keys are the well-known x86-64 psABI microarchitecture levels, but
/// the sets here are pragmatically inclusive (each level also enables the widely-available crypto/bit
/// extensions of its era, not only the strict psABI baseline) since this drives editor completion, not
/// ABI codegen.</para>
/// </summary>
public static class ArchProfileKeys
{
    /// <summary>Honor the individual ARCH_* toggles (the detailed page). Historical behavior.</summary>
    public const string Custom = "custom";

    /// <summary>x86-64-v1: baseline 8086→P6, X64, MMX, SSE, SSE2.</summary>
    public const string V1 = "v1";

    /// <summary>x86-64-v2: + SSE3, SSSE3, SSE4.1/4.2, POPCNT, CMPXCHG16B-era crypto (AES, PCLMULQDQ).</summary>
    public const string V2 = "v2";

    /// <summary>x86-64-v3: + AVX, AVX2, FMA, BMI1/2, F16C, LZCNT, MOVBE and the common Haswell-era extensions.</summary>
    public const string V3 = "v3";

    /// <summary>x86-64-v4: + the AVX-512 family (F/CD/ER/PF/BW/DQ/VL and the later AVX-512 sub-ISAs) + SHA.</summary>
    public const string V4 = "v4";

    /// <summary>Everything modern Intel: all architectures except deprecated / vendor-legacy ones
    /// (SSE4A, SSE5, generic AMD, TBM, 3DNow!, Cyrix, IA-64, undocumented).</summary>
    public const string Latest = "latest";

    /// <summary>Every architecture AsmDude knows, including deprecated and vendor-specific ones.</summary>
    public const string Everything = "everything";
}
