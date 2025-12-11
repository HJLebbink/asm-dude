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

using System.Text;
using AsmTools;
using AsmSourceTools;
using Microsoft.Extensions.Logging;
using AsmDude3.Server.Models;
using AsmDude3.Server.Providers;

namespace AsmDude3.Server.Stores;

public class MnemonicStore
{
    private readonly ILogger _logger;
    private readonly AsmLanguageServerOptions _options;

    private readonly Dictionary<Mnemonic, List<AsmSignatureInformation>> data_;
    private readonly Dictionary<Mnemonic, List<Arch>> arch_;
    private readonly Dictionary<Mnemonic, string> htmlRef_;
    private readonly Dictionary<Mnemonic, string> description_;
    private readonly HashSet<Mnemonic> mnemonics_switched_on_;
    private readonly HashSet<Rn> register_switched_on_;

    public MnemonicStore(ILogger logger, string filename_RegularData, string filename_HandcraftedData, AsmLanguageServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _logger.LogInformation("MnemonicStore: constructor: regularData = {RegularData}; handcraftedData = {HandcraftedData}",
            filename_RegularData, filename_HandcraftedData);

        var (data, arch, htmlRef, description) = CalcSignatureInformation(filename_RegularData, filename_HandcraftedData);

        data_ = data;
        arch_ = arch;
        htmlRef_ = htmlRef;
        description_ = description;
        mnemonics_switched_on_ = CalcMnemonicsSwitchedOn();
        register_switched_on_ = CalcRegisterSwitchedOn();
    }

    public bool HasElement(Mnemonic mnemonic)
    {
        return data_.ContainsKey(mnemonic);
    }

    public IEnumerable<AsmSignatureInformation> GetSignatures(Mnemonic mnemonic)
    {
        return data_.TryGetValue(mnemonic, out List<AsmSignatureInformation>? list) ? list : Enumerable.Empty<AsmSignatureInformation>();
    }

    public IEnumerable<Arch> GetArch(Mnemonic mnemonic)
    {
        return arch_.TryGetValue(mnemonic, out List<Arch>? value) ? value : Enumerable.Empty<Arch>();
    }

    public string GetHtmlRef(Mnemonic mnemonic)
    {
        return htmlRef_.TryGetValue(mnemonic, out string? value) ? value : string.Empty;
    }

    public string GetDescription(Mnemonic mnemonic)
    {
        return description_.TryGetValue(mnemonic, out string? value) ? value : string.Empty;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        foreach (KeyValuePair<Mnemonic, List<AsmSignatureInformation>> element in data_)
        {
            Mnemonic mnemonic = element.Key;
            string s1 = mnemonic.ToString();
            string s6 = htmlRef_[mnemonic];

            foreach (AsmSignatureInformation sig in element.Value)
            {
                string s2 = sig.SignatureInformation.Label;
                string s3 = "ARCH";
                string s4 = "TODO XYZZY";
                var s5 = sig.SignatureInformation.Documentation;
                sb.AppendLine(s1 + "\t" + s2 + "\t" + s3 + "\t" + s4 + "\t" + s5 + "\t" + s6);
            }
        }
        return sb.ToString();
    }

    private AsmSignatureInformation CreateAsmSignatureElement(Mnemonic mnemonic, string args, string arch, string sign, string doc)
    {
        List<AsmSignatureEnum> ParseOperands(string str)
        {
            List<AsmSignatureEnum> result = new();
            str = str.Replace("R/M", "R_M", StringComparison.Ordinal);
            foreach (string op in str.Split('/'))
            {
                result.AddRange(AsmSignatureTools.Parse_Operand_Type_Enum(op, true));
            }
            return result;
        }

        string ParamDoc(IList<AsmSignatureEnum> x)
        {
            StringBuilder argDoc = new();
            foreach (AsmSignatureEnum op in x)
            {
                argDoc.Append(AsmSignatureTools.Get_Doc(op) + " or ");
            }
            argDoc.Length -= 4;
            return argDoc.ToString();
        }

        Tuple<int, int>[] FindParamPositions(string signature)
        {
            int startPos = -1;
            for (int i = 0; i < signature.Length; ++i)
            {
                if (signature[i] == ' ')
                {
                    startPos = i + 1;
                    break;
                }
            }
            if (startPos == -1)
            {
                return Array.Empty<Tuple<int, int>>();
            }
            var result = new List<Tuple<int, int>>();
            int previousPos = startPos;
            for (int i = startPos; i < signature.Length; ++i)
            {
                if (signature[i] == ',')
                {
                    result.Add(new Tuple<int, int>(previousPos, i));
                    previousPos = i + 1;
                }
            }
            if (previousPos < signature.Length)
            {
                result.Add(new Tuple<int, int>(previousPos, signature.Length));
            }
            return result.ToArray<Tuple<int, int>>();
        }

        var parameters = new List<ParameterInformation>();
        var operands = (args.Length == 0) ? Array.Empty<string>() : args.Split(',');
        var parameterOffsets = FindParamPositions(sign);

        if (operands.Length != parameterOffsets.Length)
        {
            _logger.LogError("MnemonicStore:CreateAsmSignatureElement: inconsistent signature information: args={Args}; parameterOffsets={ParamOffsets}",
                args, parameterOffsets);
            for (int i = 0; i < operands.Length; ++i)
            {
                _logger.LogError("MnemonicStore:CreateAsmSignatureElement: operands[{Index}]={Operand}", i, operands[i]);
            }
            for (int i = 0; i < parameterOffsets.Length; ++i)
            {
                _logger.LogError("MnemonicStore:CreateAsmSignatureElement: parameterOffsets[{Index}]={Offset}; sign={Sign}",
                    i, parameterOffsets[i], sign);
            }
        }

        var operandList = new List<IList<AsmSignatureEnum>>();

        var archs = ArchTools.ParseArchList(arch, false, true);
        if (archs[0] == Arch.ARCH_NONE)
        {
            Console.WriteLine($"MnemonicStore: CreateAsmSignatureElement: arch is \"{arch}\": mnemonic={mnemonic}; doc ={doc}");
        }

        for (int j = 0; j < operands.Length; ++j)
        {
            var operand = ParseOperands(operands[j]);
            operandList.Add(operand);

            // Extract parameter label from signature using offsets
            string paramLabel = (j < parameterOffsets.Length && parameterOffsets[j].Item1 < sign.Length && parameterOffsets[j].Item2 <= sign.Length)
                ? sign.Substring(parameterOffsets[j].Item1, parameterOffsets[j].Item2 - parameterOffsets[j].Item1).Trim()
                : operands[j];

            parameters.Add(new ParameterInformation
            {
                Label = paramLabel,
                Documentation = ParamDoc(operandList[j]),
            });
        }
        return new AsmSignatureInformation
        {
            Mnemonic = mnemonic,
            Arch = archs,
            Operands = operandList,
            SignatureInformation = new SignatureInformation
            {
                Label = sign,
                Parameters = parameters,
                Documentation = doc,
            }
        };
    }

    private (Dictionary<Mnemonic, List<AsmSignatureInformation>> data, Dictionary<Mnemonic, List<Arch>> arch, Dictionary<Mnemonic, string> htmlRef, Dictionary<Mnemonic, string> description) CalcSignatureInformation(string filename_RegularData, string filename_HandcraftedData)
    {
        Dictionary<Mnemonic, List<AsmSignatureInformation>> data = new();
        Dictionary<Mnemonic, List<Arch>> arch = new();
        Dictionary<Mnemonic, string> htmlRef = new();
        Dictionary<Mnemonic, string> description = new();

        bool Add(AsmSignatureInformation asmSignatureElement, ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data)
        {
            bool result = false;

            if (data.TryGetValue(asmSignatureElement.Mnemonic, out List<AsmSignatureInformation>? signatureElementList))
            {
                result = signatureElementList.Remove(asmSignatureElement);
                signatureElementList.Add(asmSignatureElement);
            }
            else
            {
                data.Add(asmSignatureElement.Mnemonic, new List<AsmSignatureInformation> { asmSignatureElement });
            }
            return result;
        }

        void LoadRegularData(
            string filename,
            ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data,
            ref Dictionary<Mnemonic, List<Arch>> arch,
            ref Dictionary<Mnemonic, string> htmlRef,
            ref Dictionary<Mnemonic, string> description)
        {
            _logger.LogInformation("MnemonicStore:loadRegularData: filename={Filename}", filename);
            try
            {
                StreamReader file = new(filename);
                string? line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 4)
                        { // general description
                            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(columns[1], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                _logger.LogWarning("MnemonicStore:loadRegularData: unknown mnemonic in line: {Line}", line);
                            }
                            else
                            {
                                if (!description.ContainsKey(mnemonic))
                                {
                                    description.Add(mnemonic, columns[2]);
                                }
                                if (!htmlRef.ContainsKey(mnemonic))
                                {
                                    htmlRef.Add(mnemonic, columns[3]);
                                }
                            }
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                _logger.LogWarning("MnemonicStore:loadRegularData: unknown mnemonic in line: {Line}", line);
                            }
                            else
                            {
                                var se = CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                _logger.LogInformation("MnemonicStore: adding AsmSignatureInformation {Label}", se.SignatureInformation.Label);
                                if (Add(se, ref data))
                                {
                                    _logger.LogWarning("MnemonicStore:loadRegularData: signature already exists {Signature}", se.ToString());
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("MnemonicStore:loadRegularData: s.Length={Length}; funky line {Line}", columns.Length, line);
                        }
                    }
                }
                file.Close();

                foreach ((Mnemonic key, List<AsmSignatureInformation> value) in data)
                {
                    HashSet<Arch> archs = new();
                    foreach (AsmSignatureInformation signatureElement in value)
                    {
                        archs.UnionWith(signatureElement.Arch);
                    }
                    arch[key] = archs.ToList();
                }
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("MnemonicStore:loadRegularData: could not find file \"{Filename}\".", filename);
            }
            catch (Exception e)
            {
                _logger.LogError("MnemonicStore:loadRegularData: error while reading file \"{Filename}\". {Exception}", filename, e);
            }
        }

        void LoadHandcraftedData(
            string filename,
            ref Dictionary<Mnemonic, List<AsmSignatureInformation>> data,
            ref Dictionary<Mnemonic, List<Arch>> arch,
            ref Dictionary<Mnemonic, string> htmlRef,
            ref Dictionary<Mnemonic, string> description)
        {
            _logger.LogInformation("MnemonicStore:load_data_intel: filename={Filename}", filename);
            try
            {
                StreamReader file = new(filename);
                string? line;
                while ((line = file.ReadLine()) != null)
                {
                    if ((line.Length > 0) && (!line.StartsWith(";", StringComparison.Ordinal)))
                    {
                        string[] columns = line.Split('\t');
                        if (columns.Length == 4)
                        { // general description
                            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(columns[1], false);

                            if (mnemonic == Mnemonic.NONE)
                            {
                                _logger.LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line {Line}", line);
                            }
                            else
                            {
                                description.Remove(mnemonic);
                                description.Add(mnemonic, columns[2]);

                                htmlRef.Remove(mnemonic);
                                htmlRef.Add(mnemonic, columns[3]);
                            }
                        }
                        else if ((columns.Length == 5) || (columns.Length == 6))
                        { // signature description, ignore an old sixth column
                            Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(columns[0], false);
                            if (mnemonic == Mnemonic.NONE)
                            {
                                _logger.LogWarning("MnemonicStore:loadHandcraftedData: unknown mnemonic in line {Line}", line);
                            }
                            else
                            {
                                var se = CreateAsmSignatureElement(mnemonic, columns[1], columns[2], columns[3], columns[4]);
                                if (Add(se, ref data))
                                {
                                    _logger.LogWarning("MnemonicStore:LoadHandcraftedData: signature already exists {Signature}", se.ToString());
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("MnemonicStore:loadHandcraftedData: s.Length={Length}; funky line {Line}", columns.Length, line);
                        }
                    }
                }
                file.Close();

                foreach ((Mnemonic key, List<AsmSignatureInformation> value) in data)
                {
                    HashSet<Arch> archs = new();
                    foreach (AsmSignatureInformation signatureElement in value)
                    {
                        archs.UnionWith(signatureElement.Arch);
                    }
                    arch[key] = archs.ToList();
                }
            }
            catch (FileNotFoundException)
            {
                _logger.LogError("MnemonicStore:LoadHandcraftedData: could not find file \"{Filename}\".", filename);
            }
            catch (Exception e)
            {
                _logger.LogError("MnemonicStore:LoadHandcraftedData: error while reading file \"{Filename}\". {Exception}", filename, e);
            }
        }

        if (File.Exists(filename_RegularData))
        {
            LoadRegularData(filename_RegularData, ref data, ref arch, ref htmlRef, ref description);
        }
        else
        {
            _logger.LogError("MnemonicStore: constructor: regularData = {RegularData} does not exist", filename_RegularData);
        }

        if (filename_HandcraftedData != null)
        {
            if (File.Exists(filename_HandcraftedData))
            {
                LoadHandcraftedData(filename_HandcraftedData, ref data, ref arch, ref htmlRef, ref description);
            }
            else
            {
                _logger.LogError("MnemonicStore: constructor: handcraftedData = {HandcraftedData} does not exist", filename_HandcraftedData);
            }
        }
        return (data, arch, htmlRef, description);
    }

    public bool IsMnemonicSwitchedOn(Mnemonic mnemonic)
    {
        return mnemonics_switched_on_.Contains(mnemonic);
    }

    public HashSet<Mnemonic> Get_Allowed_Mnemonics()
    {
        return mnemonics_switched_on_;
    }

    private HashSet<Mnemonic> CalcMnemonicsSwitchedOn()
    {
        HashSet<Mnemonic> result = new();

        ISet<Arch> arch_switched_on = _options.Get_Arch_Switched_On();
        foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
        {
            foreach (Arch a in GetArch(mnemonic))
            {
                if (arch_switched_on.Contains(a))
                {
                    result.Add(mnemonic);
                    break;
                }
            }
        }
        return result;
    }

    public bool IsRegisterSwitchedOn(Rn reg)
    {
        return register_switched_on_.Contains(reg);
    }

    public HashSet<Rn> Get_Allowed_Registers()
    {
        return register_switched_on_;
    }

    private HashSet<Rn> CalcRegisterSwitchedOn()
    {
        HashSet<Rn> result = new();

        ISet<Arch> arch_switched_on = _options.Get_Arch_Switched_On();
        foreach (Rn reg in Enum.GetValues(typeof(Rn)))
        {
            if (reg != Rn.NOREG)
            {
                if (arch_switched_on.Contains(RegisterTools.GetArch(reg)))
                {
                    result.Add(reg);
                }
            }
        }
        return result;
    }
}
