// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2LS.Tests;

using System.Drawing;
using System.Text.Json;

using AsmTools;

using FluentAssertions;

using Xunit;

/// <summary>
/// Robustness tests for <see cref="ColorJsonConverter.Read"/> against malformed/out-of-range color
/// JSON (settings.json is user-editable and fuzzed by asm-fuzz's <c>settings</c> target). Each test
/// fails if its specific guard is removed: clamping (would throw <see cref="System.ArgumentException"/>
/// from <see cref="Color.FromArgb(int,int,int,int)"/>), the numeric-token check (would throw
/// <see cref="System.InvalidOperationException"/>), or the safe hex parse (would throw
/// <see cref="System.FormatException"/>/<see cref="System.OverflowException"/>).
/// </summary>
public class ColorJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ColorJsonConverter() } };

    private static Color Read(string json) => JsonSerializer.Deserialize<Color>(json, Options);

    [Fact]
    public void OutOfRange_ArgbComponents_AreClamped_NotThrown()
    {
        Color c = Read("""{ "R": 300, "G": -5, "B": 10, "A": 999 }""");

        c.R.Should().Be(255);
        c.G.Should().Be(0);
        c.B.Should().Be(10);
        c.A.Should().Be(255);
    }

    [Fact]
    public void NonNumeric_Component_IsIgnored_NotThrown()
    {
        Color c = Read("""{ "R": "oops", "G": 20, "B": 30 }""");

        c.R.Should().Be(0);   // non-numeric R ignored -> default 0
        c.G.Should().Be(20);
        c.B.Should().Be(30);
        c.A.Should().Be(255); // A absent -> default 255
    }

    [Fact]
    public void Malformed_HexString_FallsBackToEmpty_NotThrown()
    {
        Read("\"#zzzz\"").Should().Be(Color.Empty);
        Read("\"#FFFFFFFFFFFF\"").Should().Be(Color.Empty); // too many digits (would overflow)
    }

    [Fact]
    public void Valid_ArgbObject_RoundTripsExactly()
    {
        Color c = Read("""{ "A": 40, "R": 10, "G": 20, "B": 30 }""");

        c.A.Should().Be(40);
        c.R.Should().Be(10);
        c.G.Should().Be(20);
        c.B.Should().Be(30);
    }

    [Fact]
    public void Valid_HexRrggbb_IsParsed()
    {
        Color c = Read("\"#FF8000\"");

        c.R.Should().Be(255);
        c.G.Should().Be(128);
        c.B.Should().Be(0);
        c.A.Should().Be(255);
    }
}
