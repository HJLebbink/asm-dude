// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmAnnotate.Tests;

using asm_annotate;

using System.IO;
using System.Linq;
using System.Xml.Linq;

using Xunit;

/// <summary>
/// Unit tests for <see cref="UopsInfoImporter"/> — the uops.info XML → per-arch TSV converter. Covers the
/// mnemonic-suffix normalization, operand extraction, latency selection, and an end-to-end <c>Run</c> on a
/// synthetic instructions.xml fixture (exercising arch routing + column mapping on real production code).
/// </summary>
public class UopsInfoImporterTests
{
    [Theory]
    [InlineData("ADD", "ADD", "")]              // already a mnemonic
    [InlineData("MOV", "MOV", "")]
    [InlineData("CALL_NEAR", "CALL", "")]        // CALL has no plain iclass — must be recovered
    [InlineData("CALL_FAR", "CALL", "far")]
    [InlineData("RET_NEAR", "RET", "")]
    [InlineData("RET_FAR", "RET", "far")]
    [InlineData("ADD_LOCK", "ADD", "lock")]      // locked RMW timing
    [InlineData("XCHG_LOCK", "XCHG", "lock")]
    [InlineData("CMPSD_XMM", "CMPSD", "")]        // SSE form disambiguated from the string op
    [InlineData("MOVSD_XMM", "MOVSD", "")]
    [InlineData("VPEXTRW_C5", "VPEXTRW_C5", "")] // unknown suffix → left as-is (loader will skip)
    public void NormalizeMnemonic_StripsKnownSuffixes(string iclass, string expectedMnemonic, string expectedNote)
    {
        (string mnemonic, string note) = UopsInfoImporter.NormalizeMnemonic(iclass);

        Assert.Equal(expectedMnemonic, mnemonic);
        Assert.Equal(expectedNote, note);
    }

    [Theory]
    [InlineData("ADD (R32, R32)", "R32, R32")]
    [InlineData("PREFETCHW (M512)", "M512")]
    [InlineData("ADD (AL, 0)", "AL, 0")]
    [InlineData("FEMMS", "")]            // no parentheses → no operands
    [InlineData("", "")]
    [InlineData(null, "")]
    public void OperandsFromString_ExtractsParenthesizedOperands(string? input, string expected)
    {
        Assert.Equal(expected, UopsInfoImporter.OperandsFromString(input));
    }

    [Fact]
    public void MaxLatency_ReturnsMaxPlainCycles()
    {
        var measurement = XElement.Parse(
            "<measurement>" +
            "  <latency start_op='1' target_op='1' cycles='1'/>" +
            "  <latency start_op='1' target_op='3' cycles='4'/>" +
            "  <latency start_op='2' target_op='1' cycles='3'/>" +
            "</measurement>");

        Assert.Equal("4", UopsInfoImporter.MaxLatency(measurement));
    }

    [Fact]
    public void MaxLatency_IgnoresUpperBoundMemoryCycles()
    {
        // Store-like form: only memory/address upper-bound variants, no exact register-operand latency.
        var measurement = XElement.Parse(
            "<measurement>" +
            "  <latency start_op='1' target_op='2' cycles_mem='6' cycles_mem_is_upper_bound='1'/>" +
            "</measurement>");

        Assert.Equal(string.Empty, UopsInfoImporter.MaxLatency(measurement));
    }

    [Fact]
    public void Run_EndToEnd_EmitsRoutedRowsWithCorrectColumns()
    {
        string dir = Path.Combine(Path.GetTempPath(), "uops-test-" + Path.GetRandomFileName());
        string xml = Path.Combine(dir, "instructions.xml");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(xml,
                "<root date='2026-01-01'>" +
                "  <extension name='BASE'>" +
                "    <instruction asm='ADD' iclass='ADD' iform='ADD_GPRv_GPRv' isa-set='I86' string='ADD (R64, R64)'>" +
                "      <operand idx='1' type='reg' width='64'>RAX</operand>" +
                "      <architecture name='SKL'>" +
                "        <measurement TP_unrolled='0.25' uops='1' uops_retire_slots='1' ports='1*p0156'>" +
                "          <latency start_op='1' target_op='1' cycles='1'/>" +
                "        </measurement>" +
                "      </architecture>" +
                "      <architecture name='HSW'>" +
                "        <measurement TP_unrolled='0.30' uops='1' uops_retire_slots='1' ports='1*p0156'>" +
                "          <latency cycles='1'/>" +
                "        </measurement>" +
                "      </architecture>" +
                "    </instruction>" +
                "  </extension>" +
                "</root>");

            int rc = UopsInfoImporter.Run(xml, dir);
            Assert.Equal(0, rc);

            // SKL routed to Skylake.tsv with the right 8 columns.
            string[] skl = DataRows(Path.Combine(dir, "Skylake.tsv"));
            Assert.Single(skl);
            string[] cols = skl[0].Split('\t');
            Assert.Equal(8, cols.Length);
            Assert.Equal("ADD", cols[0]);        // Instruction
            Assert.Equal("R64, R64", cols[1]);   // operands (from string attribute)
            Assert.Equal("1", cols[2]);          // µOps fused (uops_retire_slots)
            Assert.Equal("1", cols[3]);          // µOps unfused (uops)
            Assert.Equal("1*p0156", cols[4]);    // ports
            Assert.Equal("1", cols[5]);          // latency (max cycles)
            Assert.Equal("0.25", cols[6]);       // throughput (TP_unrolled)
            Assert.Equal("I86", cols[7]);        // remark (isa-set)

            // HSW routed to its own file with that arch's throughput.
            string[] hsw = DataRows(Path.Combine(dir, "Haswell.tsv"));
            Assert.Single(hsw);
            Assert.Equal("0.30", hsw[0].Split('\t')[6]);

            // An arch absent from the fixture yields a header-only file (no data rows) — proves routing.
            Assert.Empty(DataRows(Path.Combine(dir, "Zen5.tsv")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string[] DataRows(string tsvPath) =>
        File.ReadAllLines(tsvPath).Where(l => l.Length > 0 && !l.StartsWith(';')).ToArray();
}
