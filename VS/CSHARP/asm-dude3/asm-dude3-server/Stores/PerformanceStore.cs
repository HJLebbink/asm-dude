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

using AsmTools;
using AsmSourceTools;
using Microsoft.Extensions.Logging;

namespace AsmDude3.Server.Stores;

public struct PerformanceItem : IEquatable<PerformanceItem>
{
    public MicroArch MicroArch { get; init; }
    public Mnemonic Instruction { get; init; }
    public string Args { get; init; }

    public string MuOpsMerged { get; init; }
    public string MuOpsFused { get; init; }
    public string MuOpsPort { get; init; }

    public string Latency { get; init; }
    public string Throughput { get; init; }
    public string Remark { get; init; }

    public override bool Equals(object? obj)
    {
        if (obj == null || !GetType().Equals(obj.GetType()))
        {
            return false;
        }
        else
        {
            PerformanceItem p = (PerformanceItem)obj;
            return (MicroArch == p.MicroArch) && (Instruction == p.Instruction) && (Args == p.Args);
        }
    }

    public override int GetHashCode()
    {
        return MicroArch.GetHashCode() ^ Instruction.GetHashCode() ^ Args.GetHashCode();
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
    private readonly ILogger _logger;
    private readonly AsmLanguageServerOptions _options;
    private readonly List<PerformanceItem> data_;

    public PerformanceStore(ILogger logger, string performancePath, AsmLanguageServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        data_ = new List<PerformanceItem>();

        if (_options.PerformanceInfo_On)
        {
            MicroArch selectedMicroarchitectures = _options.Get_MicroArch_Switched_On();
            if (selectedMicroarchitectures != MicroArch.NONE)
            {
                IDictionary<string, IList<Mnemonic>> translations = LoadInstructionTranslation(
                    Path.Combine(performancePath, "Instructions-Translations.tsv"));

                if (selectedMicroarchitectures.HasFlag(MicroArch.IvyBridge))
                {
                    AddData(MicroArch.IvyBridge, Path.Combine(performancePath, "IvyBridge.tsv"), translations);
                }

                if (selectedMicroarchitectures.HasFlag(MicroArch.Haswell))
                {
                    AddData(MicroArch.Haswell, Path.Combine(performancePath, "Haswell.tsv"), translations);
                }

                if (selectedMicroarchitectures.HasFlag(MicroArch.Broadwell))
                {
                    AddData(MicroArch.Broadwell, Path.Combine(performancePath, "Broadwell.tsv"), translations);
                }

                if (selectedMicroarchitectures.HasFlag(MicroArch.Skylake))
                {
                    AddData(MicroArch.Skylake, Path.Combine(performancePath, "Skylake.tsv"), translations);
                }

                if (selectedMicroarchitectures.HasFlag(MicroArch.SkylakeX))
                {
                    AddData(MicroArch.SkylakeX, Path.Combine(performancePath, "SkylakeX.tsv"), translations);
                }
            }
        }
    }

    public IEnumerable<PerformanceItem> GetPerformance(Mnemonic mnemonic, MicroArch selectedArchitectures)
    {
        foreach (PerformanceItem item in data_)
        {
            if ((item.Instruction == mnemonic) && selectedArchitectures.HasFlag(item.MicroArch))
            {
                yield return item;
            }
        }
    }

    #region Private Methods

    private void AddData(MicroArch microArch, string filename, IDictionary<string, IList<Mnemonic>> translations)
    {
        try
        {
            StreamReader file = new(filename);
            string? line;
            int lineNumber = 0;

            while ((line = file.ReadLine()) != null)
            {
                if ((line.Trim().Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                {
                    string[] columns = line.Split('\t');
                    if (columns.Length == 8)
                    {
                        // Handle instruction
                        string mnemonicKey = columns[0].Trim();
                        if (!translations.TryGetValue(mnemonicKey, out IList<Mnemonic>? mnemonics))
                        {
                            mnemonics = new List<Mnemonic>();
                            foreach (string mnemonicStr in mnemonicKey.Split(' '))
                            {
                                Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(mnemonicStr, false);
                                if (mnemonic == Mnemonic.NONE)
                                {
                                    // Check if the mnemonicStr can be translated to a list of mnemonics
                                    if (translations.TryGetValue(mnemonicStr, out IList<Mnemonic>? mnemonics2))
                                    {
                                        foreach (Mnemonic m in mnemonics2)
                                        {
                                            mnemonics.Add(m);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("PerformanceStore:AddData: microArch={MicroArch}: unknown mnemonic {Mnemonic} in line {LineNumber}",
                                            microArch, mnemonicStr, lineNumber);
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
                            data_.Add(new PerformanceItem
                            {
                                MicroArch = microArch,
                                Instruction = m,
                                Args = columns[1],
                                MuOpsFused = columns[2],
                                MuOpsMerged = columns[3],
                                MuOpsPort = columns[4],
                                Latency = columns[5],
                                Throughput = columns[6],
                                Remark = columns[7],
                            });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("PerformanceStore:AddData: found {Count} columns in line: {Line}", columns.Length, line);
                    }
                }
                lineNumber++;
            }
            file.Close();
        }
        catch (FileNotFoundException)
        {
            _logger.LogError("PerformanceStore:LoadData: could not find file \"{Filename}\"", filename);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "PerformanceStore:LoadData: error while reading file \"{Filename}\"", filename);
        }
    }

    private IDictionary<string, IList<Mnemonic>> LoadInstructionTranslation(string filename)
    {
        IDictionary<string, IList<Mnemonic>> translations = new Dictionary<string, IList<Mnemonic>>();
        try
        {
            StreamReader file = new(filename);
            string? line;
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
                            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(mnemonicStr, false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                _logger.LogWarning("PerformanceStore:Load_Instruction_Translation: key={Key}: unknown mnemonic {Mnemonic} in line: {Line}",
                                    columns[0], mnemonicStr, line);
                            }
                            else
                            {
                                values.Add(mnemonic);
                            }
                        }
                        _logger.LogDebug("PerformanceStore:Load_Instruction_Translation: key={Key} = {Values}",
                            key, String.Join(",", values));
                        if (translations.ContainsKey(key))
                        {
                            _logger.LogWarning("PerformanceStore:Load_Instruction_Translation: key={Key} in line: {Line} already used", key, line);
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
            _logger.LogError("PerformanceStore:Load_Instruction_Translation: could not find file \"{Filename}\"", filename);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "PerformanceStore:Load_Instruction_Translation: error while reading file \"{Filename}\"", filename);
        }
        return translations;
    }

    #endregion
}
