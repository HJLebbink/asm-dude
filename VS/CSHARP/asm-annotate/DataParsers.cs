// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
// Data parsers for Intel instruction sources
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AsmAnnotate
{
    /// <summary>
    /// Represents a single instruction with metadata.
    /// Used as the common format between different data sources.
    /// </summary>
    public class InstructionData
    {
        /// <summary>
        /// Mnemonic name (e.g., "ADD", "MOV", "VMOVDQA")
        /// </summary>
        public string Mnemonic { get; set; }

        /// <summary>
        /// Instruction form/variant (e.g., "ADD_GPR32d_GPR32d")
        /// Some sources provide this, others don't
        /// </summary>
        public string InstructionForm { get; set; }

        /// <summary>
        /// Operand specification (e.g., "r,r" or "r,m")
        /// </summary>
        public string Operands { get; set; }

        /// <summary>
        /// Human-readable description of the instruction
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Instruction aliases (e.g., "CMOV" might alias to "CMOVE", "CMOVZ")
        /// </summary>
        public List<string> Aliases { get; set; } = [];

        /// <summary>
        /// Performance data indexed by microarchitecture
        /// Key: architecture name (e.g., "Haswell", "IceLake")
        /// Value: performance metrics
        /// </summary>
        public Dictionary<string, PerformanceData> Performance { get; set; } = [];
    }

    /// <summary>
    /// Performance metrics for an instruction on a specific microarchitecture.
    /// </summary>
    public class PerformanceData
    {
        /// <summary>
        /// Number of μops in the fused domain (decoder stage)
        /// </summary>
        public int? FusedOps { get; set; }

        /// <summary>
        /// Number of μops in the unfused domain (execution stage)
        /// </summary>
        public int? UnfusedOps { get; set; }

        /// <summary>
        /// Execution port assignments (e.g., "p0156", "p237 p4")
        /// </summary>
        public string ExecutionPorts { get; set; }

        /// <summary>
        /// Latency in cycles (how long before result is available)
        /// </summary>
        public double? Latency { get; set; }

        /// <summary>
        /// Throughput in reciprocal cycles (how many per cycle can execute)
        /// </summary>
        public double? Throughput { get; set; }

        /// <summary>
        /// Additional comments or special cases
        /// </summary>
        public string Comments { get; set; }
    }

    /// <summary>
    /// Base class for instruction data parsers.
    /// Each parser extracts instruction data from a specific source format.
    /// </summary>
    public abstract class InstructionDataParser
    {
        /// <summary>
        /// Parse instruction data from a file or other source.
        /// </summary>
        public abstract List<InstructionData> Parse();

        /// <summary>
        /// Verify that required source files exist
        /// </summary>
        protected void ValidateSourceExists(string sourcePath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
        }

        /// <summary>
        /// Normalize instruction mnemonic (uppercase, stripped)
        /// </summary>
        protected static string NormalizeMnemonic(string mnemonic)
        {
            return mnemonic?.ToUpperInvariant().Trim() ?? "";
        }

        /// <summary>
        /// Parse double value, returning null if invalid
        /// </summary>
        protected static double? TryParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (double.TryParse(value.Trim(), out var result))
                return result;
            return null;
        }
    }

    /// <summary>
    /// Parses performance data from TSV files (Haswell, Broadwell, Skylake, etc.).
    /// Format: Tab-separated with instruction, operands, fused ops, unfused ops, ports, latency, throughput, comments
    /// </summary>
    public class PerformanceTsvParser : InstructionDataParser
    {
        private readonly string _tsvFilePath;
        private readonly string _architectureName;

        public PerformanceTsvParser(string tsvFilePath, string architectureName)
        {
            ValidateSourceExists(tsvFilePath);
            _tsvFilePath = tsvFilePath;
            _architectureName = architectureName;
        }

        public override List<InstructionData> Parse()
        {
            var instructions = new Dictionary<string, InstructionData>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var reader = new StreamReader(_tsvFilePath, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Skip comments and empty lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                            continue;

                        // Parse TSV line
                        var columns = line.Split('\t');
                        if (columns.Length < 2)
                            continue;

                        var mnemonic = NormalizeMnemonic(columns[0]);
                        if (string.IsNullOrEmpty(mnemonic))
                            continue;

                        // Get or create instruction entry
                        if (!instructions.ContainsKey(mnemonic))
                        {
                            instructions[mnemonic] = new InstructionData { Mnemonic = mnemonic };
                        }

                        var instr = instructions[mnemonic];

                        // Store operands on the instruction (if not already set)
                        if (string.IsNullOrEmpty(instr.Operands) && columns.Length > 1)
                            instr.Operands = columns[1];

                        // Parse performance data
                        var perfData = new PerformanceData
                        {
                            FusedOps = columns.Length > 2 ? int.TryParse(columns[2], out var fo) ? fo : null : null,
                            UnfusedOps = columns.Length > 3 ? int.TryParse(columns[3], out var uf) ? uf : null : null,
                            ExecutionPorts = columns.Length > 4 ? columns[4] : null,
                            Latency = columns.Length > 5 ? TryParseDouble(columns[5]) : null,
                            Throughput = columns.Length > 6 ? TryParseDouble(columns[6]) : null,
                            Comments = columns.Length > 7 ? columns[7] : null
                        };

                        instr.Performance[_architectureName] = perfData;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing TSV file: {_tsvFilePath}", ex);
            }

            return instructions.Values.ToList();
        }
    }

    /// <summary>
    /// Parses performance data from CSV files (Ice Lake format).
    /// Format: CSV with iform, regsize, mask, throughput, latency
    /// Needs conversion to standard TSV format
    /// </summary>
    public class PerformanceCsvParser : InstructionDataParser
    {
        private readonly string _csvFilePath;
        private readonly string _architectureName;

        public PerformanceCsvParser(string csvFilePath, string architectureName)
        {
            ValidateSourceExists(csvFilePath);
            _csvFilePath = csvFilePath;
            _architectureName = architectureName;
        }

        public override List<InstructionData> Parse()
        {
            var instructions = new Dictionary<string, InstructionData>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var reader = new StreamReader(_csvFilePath, Encoding.UTF8))
                {
                    string line;
                    // Skip header
                    var header = reader.ReadLine();

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        // Parse CSV line: iform,regsize,mask,throughput,latency
                        var columns = line.Split(',');
                        if (columns.Length < 5)
                            continue;

                        var iform = columns[0].Trim();
                        if (string.IsNullOrEmpty(iform))
                            continue;

                        // Extract mnemonic from iform (e.g., "ADD_GPR8_GPR8_00" → "ADD")
                        var mnemonic = ExtractMnemonicFromIForm(iform);

                        // Get or create instruction entry
                        if (!instructions.ContainsKey(mnemonic))
                        {
                            instructions[mnemonic] = new InstructionData { Mnemonic = mnemonic };
                        }

                        var instr = instructions[mnemonic];

                        // Store iform for reference (may be useful)
                        if (string.IsNullOrEmpty(instr.InstructionForm))
                            instr.InstructionForm = iform;

                        // Parse performance data
                        var perfData = new PerformanceData
                        {
                            Throughput = TryParseDouble(columns[3]),
                            Latency = TryParseDouble(columns[4]),
                            Comments = iform // Store iform as comment for now
                        };

                        // Key by iform to avoid overwriting if same mnemonic appears multiple times
                        instr.Performance[$"{_architectureName}_{iform}"] = perfData;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing CSV file: {_csvFilePath}", ex);
            }

            return instructions.Values.ToList();
        }

        /// <summary>
        /// Extract mnemonic from instruction form.
        /// Examples: "ADD_GPR8_GPR8_00" → "ADD", "MOV_GPR32d_GPR32d" → "MOV"
        /// </summary>
        private static string ExtractMnemonicFromIForm(string iform)
        {
            // Split on underscore and take first part
            var parts = iform.Split('_');
            return NormalizeMnemonic(parts[0]);
        }
    }

    /// <summary>
    /// Parser for Intel Intrinsics Guide XML format.
    /// TODO: Implement XML parsing for intrinsics data
    /// </summary>
    public class IntrinsicsGuideParser : InstructionDataParser
    {
        private readonly string _xmlFilePath;

        public IntrinsicsGuideParser(string xmlFilePath)
        {
            ValidateSourceExists(xmlFilePath);
            _xmlFilePath = xmlFilePath;
        }

        public override List<InstructionData> Parse()
        {
            var instructions = new List<InstructionData>();

            try
            {
                // TODO: Implement XML parsing
                // Intel Intrinsics Guide format:
                // <intrinsic name="..." return_type="..." category="...">
                //   <operation>...</operation>
                //   <performance arch="...">
                //     <latency value="..." />
                //     <throughput value="..." />
                //   </performance>
                // </intrinsic>

                // For now, return empty list
                return instructions;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing Intrinsics Guide XML: {_xmlFilePath}", ex);
            }
        }
    }

    /// <summary>
    /// Parser for Intel XED (X86 Encoder Decoder) format.
    /// TODO: Implement XED file parsing
    /// </summary>
    public class XedParser : InstructionDataParser
    {
        private readonly string _xedDatabasePath;

        public XedParser(string xedDatabasePath)
        {
            ValidateSourceExists(xedDatabasePath);
            _xedDatabasePath = xedDatabasePath;
        }

        public override List<InstructionData> Parse()
        {
            var instructions = new List<InstructionData>();

            try
            {
                // TODO: Implement XED parsing
                // XED format is complex - files are in a specific directory structure
                // Main files: xed-instructions.txt, xed-state.txt, etc.
                //
                // Example instruction entry:
                // INSTRUCTION: ADD
                // IFORMFL: ADD_LOCK | ADD_NOT_LOCK | ...
                // PATTERNS: ...
                // OPERANDS: REG MODRM | ...

                // For now, return empty list
                return instructions;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing XED database: {_xedDatabasePath}", ex);
            }
        }
    }
}
