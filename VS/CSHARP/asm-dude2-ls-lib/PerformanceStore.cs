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

namespace AsmDude2LS
{
    using AsmTools;

    using System;
    using System.Collections.Generic;
    using System.IO;

    public struct PerformanceItem : IEquatable<PerformanceItem>
    {
        public MicroArch microArch_;
        public Mnemonic instr_;
        public string args_;

        public string mu_Ops_Merged_;
        public string mu_Ops_Fused_;
        public string mu_Ops_Port_;

        public string latency_;
        public string throughput_;
        public string remark_;

        public override readonly bool Equals(object? obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                PerformanceItem p = (PerformanceItem)obj;
                return (this.microArch_ == p.microArch_) && (this.instr_ == p.instr_) && (this.args_ == p.args_);
            }
        }

        public override readonly int GetHashCode()
        {
            return this.microArch_.GetHashCode() ^ this.instr_.GetHashCode() ^ this.args_.GetHashCode();
        }

        public static bool operator ==(PerformanceItem left, PerformanceItem right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PerformanceItem left, PerformanceItem right)
        {
            return !(left == right);
        }

        public readonly bool Equals(PerformanceItem other)
        {
            return this == other;
        }
    }

    public class PerformanceStore
    {
        private readonly AsmLanguageServerOptions options;
        private readonly IList<PerformanceItem> data_;

        /// <summary>
        /// Maps each supported microarchitecture to its performance TSV filename (in <c>Resources/Performance/</c>).
        /// Filenames mirror the <see cref="MicroArch"/> member names. Data is generated from uops.info
        /// (see asm-annotate's <c>perf-uops</c> command).
        /// </summary>
        private static readonly IReadOnlyList<(MicroArch arch, string file)> ArchFiles =
        [
            (MicroArch.Conroe, "Conroe.tsv"),
            (MicroArch.Wolfdale, "Wolfdale.tsv"),
            (MicroArch.Nehalem, "Nehalem.tsv"),
            (MicroArch.Westmere, "Westmere.tsv"),
            (MicroArch.SandyBridge, "SandyBridge.tsv"),
            (MicroArch.IvyBridge, "IvyBridge.tsv"),
            (MicroArch.Haswell, "Haswell.tsv"),
            (MicroArch.Broadwell, "Broadwell.tsv"),
            (MicroArch.Skylake, "Skylake.tsv"),
            (MicroArch.SkylakeX, "SkylakeX.tsv"),
            (MicroArch.Kabylake, "Kabylake.tsv"),
            (MicroArch.CoffeeLake, "CoffeeLake.tsv"),
            (MicroArch.Cannonlake, "Cannonlake.tsv"),
            (MicroArch.CascadeLake, "CascadeLake.tsv"),
            (MicroArch.Icelake, "Icelake.tsv"),
            (MicroArch.Tigerlake, "Tigerlake.tsv"),
            (MicroArch.RocketLake, "RocketLake.tsv"),
            (MicroArch.EmeraldRapids, "EmeraldRapids.tsv"),
            (MicroArch.Bonnell, "Bonnell.tsv"),
            (MicroArch.Airmont, "Airmont.tsv"),
            (MicroArch.Goldmont, "Goldmont.tsv"),
            (MicroArch.GoldmontPlus, "GoldmontPlus.tsv"),
            (MicroArch.Tremont, "Tremont.tsv"),
            (MicroArch.Zen2, "Zen2.tsv"),
            (MicroArch.Zen3, "Zen3.tsv"),
            (MicroArch.Zen4, "Zen4.tsv"),
            (MicroArch.Zen5, "Zen5.tsv"),
        ];

        /// <summary>
    /// Constructor loads performance data from the uops.info-derived TSV files for the selected
    /// microarchitectures (one file per arch, see <see cref="ArchFiles"/>).
    /// </summary>
    /// <param name="path">Directory containing the performance TSV files (Haswell.tsv, Skylake.tsv, …).</param>
    /// <param name="options">AsmLanguageServerOptions with PerformanceInfo_On flag and selected microarchitectures.</param>
    /// <remarks>
    /// If PerformanceInfo_On is false or no microarchitectures are selected, data_ remains empty.
    /// Each TSV row's first column is a single mnemonic (the importer already normalized it), so it is
    /// parsed directly — no name-translation table is needed.
    /// </remarks>
    /// <!-- LLM-ANNOTATION -->
    /// LLM KEYWORDS: performance data loading, microarchitecture, TSV parsing, uops.info
    /// USED IN: LanguageServer.Initialize
    /// SEE ALSO: GetPerformance, AddData
    public PerformanceStore(string path, AsmLanguageServerOptions options)
        {
            this.options = options;
            this.data_ = [];

            if (this.options.PerformanceInfo_On)
            {
                MicroArch selectedMicroarchitures = this.options.Get_MicroArch_Switched_On();
                if (selectedMicroarchitures != MicroArch.NONE)
                {
                    foreach ((MicroArch arch, string file) in ArchFiles)
                    {
                        if (selectedMicroarchitures.HasFlag(arch))
                        {
                            this.AddData(arch, Path.Combine(path, file));
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Gets performance data for a specific mnemonic across selected microarchitectures.
        /// Returns all matching PerformanceItem entries matching both mnemonic and architecture.
        /// </summary>
        /// <param name="mnemonic">Instruction mnemonic to query.</param>
        /// <param name="selectedArchitectures">Microarchitectures to include (flag combination).</param>
        /// <returns>Sequence of PerformanceItem entries for matching instructions.</returns>
        /// <remarks>
        /// Used to show performance data (latency, throughput, µops) in hover_tooltips and inlay hints.
        /// Iterates data_ list and yields items matching both mnemonic and architecture flags.
        /// </remarks>
        /// <example>
        /// var perf = performanceStore.GetPerformance(Mnemonic.MOV, MicroArch.Haswell | MicroArch.Skylake);
        /// foreach (var item in perf) {
        ///     Console.WriteLine($"Haswell latency: {item.latency_}");
        /// }
        /// </example>
        /// <!-- LLM-ANNOTATION -->
        /// LLM KEYWORDS: performance data, instruction timing, latency, throughput, microarchitecture
        /// USED IN: LanguageServer.GetHover, LanguageServer.GetInlayHints
        /// SEE ALSO: PerformanceItem, latency_, throughput_, mu_Ops_Fused_
        public IEnumerable<PerformanceItem> GetPerformance(Mnemonic mnemonic, MicroArch selectedArchitectures)
        {
            foreach (PerformanceItem item in this.data_)
            {
                if ((item.instr_ == mnemonic) && selectedArchitectures.HasFlag(item.microArch_))
                {
                    yield return item;
                }
            }
        }

        #region Private Methods

        /// <summary>
        /// Reads one 8-column TSV (Instruction, operands, µOps-fused, µOps-unfused, ports, latency,
        /// throughput, remark) and appends a <see cref="PerformanceItem"/> per row. The first column is a
        /// single mnemonic; rows whose mnemonic is unknown to <see cref="Mnemonic"/> are skipped with a warning.
        /// </summary>
        private void AddData(MicroArch microArch, string filename)
        {
            try
            {
                using StreamReader file = new(filename);
                string? line;

                while ((line = file.ReadLine()) is not null)
                {
                    if ((line.Trim().Length == 0) || line.StartsWith(';'))
                    {
                        continue;
                    }

                    string[] columns = line.Split('\t');
                    if (columns.Length != 8)
                    {
                        AsmDudeLog.Warning("PerformanceStore:AddData: expected 8 columns, found " + columns.Length + " in line: " + line);
                        continue;
                    }

                    Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(columns[0].Trim(), false);
                    if (mnemonic == Mnemonic.NONE)
                    {
                        AsmDudeLog.Warning("PerformanceStore:AddData: microArch=" + microArch + ": unknown mnemonic \"" + columns[0].Trim() + "\" in line: " + line);
                        continue;
                    }

                    this.data_.Add(new PerformanceItem()
                    {
                        microArch_ = microArch,
                        instr_ = mnemonic,
                        args_ = columns[1],
                        mu_Ops_Fused_ = columns[2],
                        mu_Ops_Merged_ = columns[3],
                        mu_Ops_Port_ = columns[4],
                        latency_ = columns[5],
                        throughput_ = columns[6],
                        remark_ = columns[7],
                    });
                }
            }
            catch (FileNotFoundException)
            {
                AsmDudeLog.Error("PerformanceStore:AddData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                AsmDudeLog.Error("PerformanceStore:AddData: error while reading file \"" + filename + "\"." + e);
            }
        }
        #endregion
    }
}
