using AsmDude.SignatureHelp;
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

        public void AddData_New(MicroArch microArch, string filename)
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

        public void AddData(MicroArch microArch, string filename)
        {
            AsmDudeToolsStatic.Output_INFO("PerformanceStore:AddData: microArch=" + microArch + "; filename=" + filename);
            try
            {
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Trim().Length > 0) && (!line.StartsWith(";")))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 5)
                        {
                            { // handle instruction
                                string mnemonicKey = columns[0].Trim();
                                foreach (string mnemonicStr in mnemonicKey.Split(' '))
                                {
                                    if (mnemonicStr.Length > 0)
                                    {
                                        Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr);
                                        if (mnemonic == Mnemonic.UNKNOWN)
                                        {
                                            AsmDudeToolsStatic.Output_WARNING("PerformanceStore:LoadData: microArch=" + microArch + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
                                        }
                                        else
                                        {
                                            this._data.Add(new PerformanceItem()
                                            {
                                                _microArch = microArch,
                                                _instr = mnemonic,
                                                _args = columns[1],
                                                _latency = columns[2],
                                                _throughput = columns[3],
                                                _remark = columns[4]
                                            });
                                        }
                                    }
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
                            AsmDudeToolsStatic.Output_INFO("PerformanceStore:Load_Instruction_Translation: key=" + key + " = " + String.Join(",", values));
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
