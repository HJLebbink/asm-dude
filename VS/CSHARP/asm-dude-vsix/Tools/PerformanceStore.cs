// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmTools;
using System;
using System.Collections.Generic;
using System.IO;

namespace AsmDude.Tools
{
    public struct PerformanceItem
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
    }

    public class PerformanceStore
    {
        private readonly IList<PerformanceItem> _data;
        private readonly IDictionary<string, IList<Mnemonic>> _instruction_Translation;


        public PerformanceStore()
        {
            this._data = new List<PerformanceItem>();
            this._instruction_Translation = new Dictionary<string, IList<Mnemonic>>();
        }

        public void Clear()
        {
            this._data.Clear();
            this._instruction_Translation.Clear();
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

        public void AddData(MicroArch microArch, string filename)
        {
            AsmDudeToolsStatic.Output_INFO("PerformanceStore:AddData_New: microArch=" + microArch + "; filename=" + filename);
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";")))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 8)
                        {
                            { // handle instruction
                                string mnemonicKey = columns[0].Trim();
                                if (!this._instruction_Translation.TryGetValue(mnemonicKey, out IList<Mnemonic> mnemonics))
                                {
                                    mnemonics = new List<Mnemonic>();
                                    foreach (string mnemonicStr in mnemonicKey.Split(' '))
                                    {
                                        Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr);
                                        if (mnemonic == Mnemonic.UNKNOWN)
                                        {
                                            AsmDudeToolsStatic.Output_WARNING("PerformanceStore:LoadData: microArch=" + microArch + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
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
                                        _remark = columns[7]
                                    });
                                }
                            }
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_WARNING("PerformanceStore:AddData: found " + columns.Length + " columns; funky line" + line);
                        }
                    }
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

        public void Load_Instruction_Translation(string filename)
        {
            this._instruction_Translation.Clear();
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
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
                                if (mnemonic == Mnemonic.UNKNOWN)
                                {
                                    AsmDudeToolsStatic.Output_WARNING("PerformanceStore:LoadData: key=" + columns[0] + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
                                }
                                else
                                {
                                    values.Add(mnemonic);
                                }
                            }
                            //AsmDudeToolsStatic.Output_INFO("PerformanceStore:Load_Instruction_Translation: key=" + key + " = " + String.Join(",", values));
                            this._instruction_Translation.Add(key, values);
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
        }
    }
}
