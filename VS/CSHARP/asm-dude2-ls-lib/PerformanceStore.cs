// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
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
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using AsmTools;

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

        public override bool Equals(object obj)
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

        public override int GetHashCode()
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

        public bool Equals(PerformanceItem other)
        {
            return this == other;
        }
    }

    public class PerformanceStore
    {
        private readonly TraceSource traceSource;
        private readonly AsmLanguageServerOptions options;
        private readonly IList<PerformanceItem> data_;

        public PerformanceStore(string path, TraceSource traceSource, AsmLanguageServerOptions options)
        {
            this.traceSource = traceSource;
            this.options = options;
            this.data_ = new List<PerformanceItem>();

            if (this.options.PerformanceInfo_On)
            {
                MicroArch selectedMicroarchitures = this.options.Get_MicroArch_Switched_On();
                if (selectedMicroarchitures != MicroArch.NONE)
                {
                    IDictionary<string, IList<Mnemonic>> translations = this.Load_Instruction_Translation(Path.Combine(path, "Instructions-Translations.tsv"));
                    if (selectedMicroarchitures.HasFlag(MicroArch.IvyBridge))
                    {
                        this.AddData(MicroArch.IvyBridge, Path.Combine(path, "IvyBridge.tsv"), translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Haswell))
                    {
                        this.AddData(MicroArch.Haswell, Path.Combine(path, "Haswell.tsv"), translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Broadwell))
                    {
                        this.AddData(MicroArch.Broadwell, Path.Combine(path, "Broadwell.tsv"), translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Skylake))
                    {
                        this.AddData(MicroArch.Skylake, Path.Combine(path, "Skylake.tsv"), translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.SkylakeX))
                    {
                        this.AddData(MicroArch.SkylakeX, Path.Combine(path, "SkylakeX.tsv"), translations);
                    }
                }
            }
        }

        private void LogInfo(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, msg);
        }
        private void LogWarning(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Warning, 0, msg);
        }
        private void LogError(string msg)
        {
            this.traceSource.TraceEvent(TraceEventType.Error, 0, msg);
        }

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
        private void AddData(MicroArch microArch, string filename, IDictionary<string, IList<Mnemonic>> translations)
        {
            //AsmDudeToolsStatic.Output_INFO("PerformanceStore:AddData_New: microArch=" + microArch + "; filename=" + filename);
            try
            {
                StreamReader file = new StreamReader(filename);
                string line;
                int lineNumber = 0;

                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 8)
                        {
                            { // handle instruction
                                string mnemonicKey = columns[0].Trim();
                                if (!translations.TryGetValue(mnemonicKey, out IList<Mnemonic> mnemonics))
                                {
                                    mnemonics = new List<Mnemonic>();
                                    foreach (string mnemonicStr in mnemonicKey.Split(' '))
                                    {
                                        Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr, false);
                                        if (mnemonic == Mnemonic.NONE)
                                        { // check if the mnemonicStr can be translated to a list of mnemonics
                                            if (translations.TryGetValue(mnemonicStr, out IList<Mnemonic> mnemonics2))
                                            {
                                                foreach (Mnemonic m in mnemonics2)
                                                {
                                                    mnemonics.Add(m);
                                                }
                                            }
                                            else
                                            {
                                                LogWarning("PerformanceStore:AddData: microArch=" + microArch + ": unknown mnemonic " + mnemonicStr + " in line " + lineNumber + " with content \"" + line + "\".");
                                            }
                                        }
                                        else
                                        {
                                            mnemonics.Add(mnemonic);
                                        }
                                    }
                                }
                                foreach (Mnemonic m in mnemonics)
                                {
                                    this.data_.Add(new PerformanceItem()
                                    {
                                        microArch_ = microArch,
                                        instr_ = m,
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
                        }
                        else
                        {
                            LogWarning("PerformanceStore:AddData: found " + columns.Length + " columns; funky line" + line);
                        }
                    }
                    lineNumber++;
                }
                file.Close();
            }
            catch (FileNotFoundException)
            {
                LogError("PerformanceStore:LoadData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                LogError("PerformanceStore:LoadData: error while reading file \"" + filename + "\"." + e);
            }
        }

        private IDictionary<string, IList<Mnemonic>> Load_Instruction_Translation(string filename)
        {
            IDictionary<string, IList<Mnemonic>> translations = new Dictionary<string, IList<Mnemonic>>();
            try
            {
                StreamReader file = new StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 2)
                        {
                            string key = columns[0].Trim();

                            IList<Mnemonic> values = new List<Mnemonic>();
                            foreach (string mnemonicStr in columns[1].Trim().Split(' '))
                            {
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr, false);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    LogWarning("PerformanceStore:Load_Instruction_Translation: key=" + columns[0] + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
                                }
                                else
                                {
                                    values.Add(mnemonic);
                                }
                            }
                            //AsmDudeToolsStatic.Output_INFO("PerformanceStore:Load_Instruction_Translation: key=" + key + " = " + String.Join(",", values));
                            if (translations.ContainsKey(key))
                            {
                                LogWarning("PerformanceStore:Load_Instruction_Translation: key=" + key + " in line: " + line + " already used");
                            }
                            else
                            {
                                translations.Add(key, values);
                            }
                        }
                    }
                }
                file.Close();
            }
            catch (FileNotFoundException)
            {
                LogError("PerformanceStore:Load_Instruction_Translation: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                LogError("PerformanceStore:Load_Instruction_Translation: error while reading file \"" + filename + "\"." + e);
            }
            return translations;
        }
        #endregion
    }
}
