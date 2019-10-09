// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmDude.Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AsmTools;

    public struct PerformanceItem : IEquatable<PerformanceItem>
    {
        public MicroArch _microArch;
        public Mnemonic _instr;
        public string _args;

        public string _mu_Ops_Merged;
        public string _mu_Ops_Fused;
        public string _mu_Ops_Port;

        public string _latency;
        public string _throughput;
        public string _remark;

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
                return (this._microArch == p._microArch) && (this._instr == p._instr) && (this._args == p._args);
            }
        }

        public override int GetHashCode()
        {
            return this._microArch.GetHashCode() ^ this._instr.GetHashCode() ^ this._args.GetHashCode();
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
        private readonly IList<PerformanceItem> _data;

        public PerformanceStore(string path)
        {
            this._data = new List<PerformanceItem>();

            if (Settings.Default.PerformanceInfo_On)
            {
                MicroArch selectedMicroarchitures = AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                if (selectedMicroarchitures != MicroArch.NONE)
                {
                    IDictionary<string, IList<Mnemonic>> translations = this.Load_Instruction_Translation(path + "Instructions-Translations.tsv");
                    if (selectedMicroarchitures.HasFlag(MicroArch.IvyBridge))
                    {
                        this.AddData(MicroArch.IvyBridge, path + "IvyBridge.tsv", translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Haswell))
                    {
                        this.AddData(MicroArch.Haswell, path + "Haswell.tsv", translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Broadwell))
                    {
                        this.AddData(MicroArch.Broadwell, path + "Broadwell.tsv", translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.Skylake))
                    {
                        this.AddData(MicroArch.Skylake, path + "Skylake.tsv", translations);
                    }

                    if (selectedMicroarchitures.HasFlag(MicroArch.SkylakeX))
                    {
                        this.AddData(MicroArch.SkylakeX, path + "SkylakeX.tsv", translations);
                    }
                }
            }
        }

        public IEnumerable<PerformanceItem> GetPerformance(Mnemonic mnemonic, MicroArch selectedArchitectures)
        {
            foreach (PerformanceItem item in this._data)
            {
                if ((item._instr == mnemonic) && selectedArchitectures.HasFlag(item._microArch))
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
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";")))
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
                                        Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr);
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
                                                AsmDudeToolsStatic.Output_WARNING("PerformanceStore:AddData: microArch=" + microArch + ": unknown mnemonic " + mnemonicStr + " in line " + lineNumber + " with content \"" + line + "\".");
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
                                    this._data.Add(new PerformanceItem()
                                    {
                                        _microArch = microArch,
                                        _instr = m,
                                        _args = columns[1],
                                        _mu_Ops_Fused = columns[2],
                                        _mu_Ops_Merged = columns[3],
                                        _mu_Ops_Port = columns[4],
                                        _latency = columns[5],
                                        _throughput = columns[6],
                                        _remark = columns[7],
                                    });
                                }
                            }
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_WARNING("PerformanceStore:AddData: found " + columns.Length + " columns; funky line" + line);
                        }
                    }
                    lineNumber++;
                }
                file.Close();
            }
            catch (FileNotFoundException)
            {
                AsmDudeToolsStatic.Output_ERROR("PerformanceStore:LoadData: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("PerformanceStore:LoadData: error while reading file \"" + filename + "\"." + e);
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
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";")))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 2)
                        {
                            string key = columns[0].Trim();

                            IList<Mnemonic> values = new List<Mnemonic>();
                            foreach (string mnemonicStr in columns[1].Trim().Split(' '))
                            {
                                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    AsmDudeToolsStatic.Output_WARNING("PerformanceStore:Load_Instruction_Translation: key=" + columns[0] + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
                                }
                                else
                                {
                                    values.Add(mnemonic);
                                }
                            }
                            //AsmDudeToolsStatic.Output_INFO("PerformanceStore:Load_Instruction_Translation: key=" + key + " = " + String.Join(",", values));
                            if (translations.ContainsKey(key))
                            {
                                AsmDudeToolsStatic.Output_WARNING("PerformanceStore:Load_Instruction_Translation: key=" + key + " in line: " + line + " already used");
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
                AsmDudeToolsStatic.Output_ERROR("PerformanceStore:Load_Instruction_Translation: could not find file \"" + filename + "\".");
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR("PerformanceStore:Load_Instruction_Translation: error while reading file \"" + filename + "\"." + e);
            }
            return translations;
        }
        #endregion
    }
}
