using AsmDude.SignatureHelp;
using AsmTools;
using System;
using System.Collections.Generic;
using System.IO;

namespace AsmDude.Tools
{

    public struct PerformanceItem
    {
        public string _microArch;
        public Mnemonic _instr;
        public string _args;
        public string _latency;
        public string _throughput;
        public string _remark;
    }

    public class PerformanceStore
    {
        private readonly IList<PerformanceItem> _data;

        public PerformanceStore()
        {
            this._data = new List<PerformanceItem>();
        }

        public void Clear()
        {
            this._data.Clear();
        }

        public IReadOnlyList<PerformanceItem> GetPerformance(Mnemonic mnemonic)
        {
            List<PerformanceItem> result = new List<PerformanceItem>();
            foreach (PerformanceItem item in this._data)
            {
                if (item._instr == mnemonic)
                {
                    result.Add(item);
                }
            }
            if (result.Count == 0)
            {
                AsmDudeToolsStatic.Output_INFO("PerformanceStore:GetPerformance: mnemonic " + mnemonic + " has no performance info");
            }

            return result.AsReadOnly();
        }

        public void AddData(string microArch, string filename)
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
                                foreach (string mnemonicStr in columns[0].Split(' '))
                                {
                                    if (mnemonicStr.Length > 0)
                                    {
                                        Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(mnemonicStr);
                                        if (mnemonic == Mnemonic.UNKNOWN)
                                        {
                                            AsmDudeToolsStatic.Output_WARNING("PerformanceStore:LoadData: microArch="+ microArch + ": unknown mnemonic " + mnemonicStr + " in line: " + line);
                                        }
                                        else
                                        {
                                            PerformanceItem item = new PerformanceItem();
                                            item._microArch = microArch;
                                            item._instr = mnemonic;
                                            item._args = columns[1];
                                            item._latency = columns[2];
                                            item._throughput = columns[3];
                                            item._remark = columns[4];

                                            this._data.Add(item);
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
    }
}
