using AsmTools;
using AsmSourceTools;
using AsmDude3.Server.Models;
using AsmDude3.Server.Stores;
using Microsoft.Extensions.Logging;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides code completion (IntelliSense) for assembly language
/// Matches AsmDude2's GetTextDocumentCompletion implementation
/// </summary>
public class CompletionProvider
{
    private readonly ILogger _logger;
    private readonly AsmDude2Tools _asmDudeTools;
    private readonly MnemonicStore _mnemonicStore;
    private readonly AsmLanguageServerOptions _options;

    private const int MAX_LENGTH_DESCR_TEXT = 40;

    public CompletionProvider(ILogger logger, AsmDude2Tools asmDudeTools,
                              MnemonicStore mnemonicStore, AsmLanguageServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _asmDudeTools = asmDudeTools ?? throw new ArgumentNullException(nameof(asmDudeTools));
        _mnemonicStore = mnemonicStore ?? throw new ArgumentNullException(nameof(mnemonicStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Provide completion suggestions for the given line and position
    /// Matches AsmDude2's GetTextDocumentCompletion implementation
    /// </summary>
    public InternalCompletionList? ProvideCompletions(string[] lines, int lineNumber, int position, LabelGraph? labelGraph)
    {
        if (lines == null || lineNumber < 0 || lineNumber >= lines.Length)
        {
            return null;
        }

        if (!_options.CodeCompletion_On)
        {
            return new InternalCompletionList { Items = Array.Empty<CompletionItem>() };
        }

        string completeLineStr = lines[lineNumber];
        int pos = position;

        // We only consider the line till (and including) the current position
        string lineStr = completeLineStr[..pos];

        int fileID = 0;
        (object _, string label, Mnemonic mnemonic, string[] args, string remark) =
            AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);

        // If we are typing in a remark: no code completion please
        if (remark.Length > 0)
        {
            return new InternalCompletionList { Items = Array.Empty<CompletionItem>() };
        }

        // Determine if the current word we are typing is all capitals
        (string currentWord, _, _) = GetWord(pos - 1, lineStr);
        bool useCapitals = (currentWord == currentWord.ToUpper());

        // If the mnemonic is NONE we should suggest mnemonics
        if (mnemonic == Mnemonic.NONE)
        {
            HashSet<AsmTokenType> selected = new() { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
            return new InternalCompletionList
            {
                Items = Selected_Completions(useCapitals, selected, true).ToArray(),
            };
        }

        int mnemonicOffsetStart = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
        if (mnemonicOffsetStart == -1)
        {
            _logger.LogError("CompletionProvider: mnemonic not found in line");
            return null;
        }

        // Are we with the cursor in the mnemonic: then we should only suggest mnemonics:
        int mnemonicOffsetEnd = mnemonicOffsetStart + mnemonic.ToString().Length;
        if (pos <= mnemonicOffsetEnd)
        {
            HashSet<AsmTokenType> selected = new() { AsmTokenType.Jump, AsmTokenType.Mnemonic };
            return new InternalCompletionList
            {
                Items = Selected_Completions(useCapitals, selected, true).ToArray(),
            };
        }

        // If the mnemonic is a jump, we should suggest labels
        if (AsmTools.AsmSourceTools.IsJump(mnemonic))
        {
            if (labelGraph != null)
            {
                return new InternalCompletionList
                {
                    Items = Label_Completions(labelGraph, useCapitals, true).ToArray(),
                };
            }
            else
            {
                return new InternalCompletionList { Items = Array.Empty<CompletionItem>() };
            }
        }

        // If we are here: there is a mnemonic, and not a jump, the cursor is not in the mnemonic,
        // thus we analyse the parameters of the mnemonic and make suggestions based on the allowed parameters

        HashSet<Arch> arch_switched_on = _options.Get_Arch_Switched_On();
        HashSet<AsmSignatureEnum> allowed = new();
        List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
        int nCommas = Math.Max(0, operands.Count - 1);

        IEnumerable<AsmSignatureInformation> allSignatures = _mnemonicStore.GetSignatures(mnemonic);

        // Constrain allSignatures of the mnemonic based on 1] architectures that are switched on, and 2] the already provided operands
        foreach (AsmSignatureInformation se in Constrain_Signatures(allSignatures, operands, arch_switched_on))
        {
            if (nCommas < se.Operands.Count)
            {
                foreach (AsmSignatureEnum s in se.Operands[nCommas])
                {
                    allowed.Add(s);
                }
            }
        }

        return new InternalCompletionList
        {
            Items = Mnemonic_Operand_Completions(useCapitals, allowed, lineNumber).ToArray()
        };
    }

    #region Helper Methods

    private IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, HashSet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
    {
        HashSet<CompletionItem> completions = new();

        // Add the completions of AsmDude directives (such as code folding directives)
        if (addSpecialKeywords && _options.CodeFolding_On)
        {
            {
                string labelText = _options.CodeFolding_BeginTag;     //the characters that start the outlining region
                completions.Add(new CompletionItem
                {
                    Kind = CompletionItemKind.Snippet,
                    Label = $"{labelText} - keyword to start code folding",
                    InsertText = labelText[1..], // remove the prefix #
                    SortText = labelText,
                });
            }
            {
                string labelText = _options.CodeFolding_EndTag;       //the characters that end the outlining region
                completions.Add(new CompletionItem
                {
                    Kind = CompletionItemKind.Snippet,
                    Label = $"{labelText} - keyword to end code folding",
                    InsertText = labelText[1..], // remove the prefix #
                    SortText = labelText,
                });
            }
        }

        AssemblerEnum usedAssembler = _options.Used_Assembler;

        // Add mnemonic completions
        if (selectedTypes.Contains(AsmTokenType.Mnemonic))
        {
            foreach (Mnemonic mnemonic2 in _mnemonicStore.Get_Allowed_Mnemonics())
            {
                string keyword_uppercase = mnemonic2.ToString();
                string insertionText = useCapitals ? keyword_uppercase : keyword_uppercase.ToLowerInvariant();
                string archStr = ArchTools.ToString(_mnemonicStore.GetArch(mnemonic2));

                completions.Add(new CompletionItem
                {
                    Kind = CompletionItemKind.Keyword,
                    Label = $"{keyword_uppercase} {archStr}",
                    InsertText = insertionText,
                    SortText = insertionText,
                    Documentation = _mnemonicStore.GetDescription(mnemonic2),
                });
            }
        }

        // Add the completions that are defined in the xml file
        foreach (string keyword_uppercase in _asmDudeTools.Get_Keywords())
        {
            AsmTokenType type = _asmDudeTools.Get_Token_Type_Intel(keyword_uppercase);
            if (selectedTypes.Contains(type))
            {
                Arch arch = Arch.ARCH_NONE;
                bool selected = true;

                if (type == AsmTokenType.Directive)
                {
                    AssemblerEnum assembler = _asmDudeTools.Get_Assembler(keyword_uppercase);
                    if (assembler.HasFlag(AssemblerEnum.MASM))
                    {
                        if (!usedAssembler.HasFlag(AssemblerEnum.MASM))
                        {
                            selected = false;
                        }
                    }
                    else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                    {
                        if (!usedAssembler.HasFlag(AssemblerEnum.NASM_INTEL))
                        {
                            selected = false;
                        }
                    }
                }
                else
                {
                    arch = _asmDudeTools.Get_Architecture(keyword_uppercase);
                    selected = _options.Is_Arch_Switched_On(arch);
                }

                if (selected)
                {
                    // by default, the entry.Key is with capitals
                    string insertionText = useCapitals ? keyword_uppercase : keyword_uppercase.ToLowerInvariant();
                    string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                    string descriptionStr = _asmDudeTools.Get_Description(keyword_uppercase);
                    descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                    string displayTextFull = keyword_uppercase + archStr + descriptionStr;
                    string displayText = Truncate(displayTextFull);

                    completions.Add(new CompletionItem
                    {
                        Kind = GetCompletionItemKind(type),
                        Label = displayText,
                        InsertText = insertionText,
                        SortText = insertionText,
                        Documentation = descriptionStr
                    });
                }
            }
        }

        return completions;
    }

    private HashSet<CompletionItem> Mnemonic_Operand_Completions(bool useCapitals, HashSet<AsmSignatureEnum> allowedOperands, int lineNumber)
    {
        bool att_Syntax = _options.Used_Assembler == AssemblerEnum.NASM_ATT;

        HashSet<CompletionItem> completions = new();

        foreach (Rn regName in _mnemonicStore.Get_Allowed_Registers())
        {
            if (AsmSignatureTools.Is_Allowed_Reg(regName, allowedOperands))
            {
                string keyword = regName.ToString();

                if (att_Syntax)
                {
                    keyword = "%" + keyword;
                }

                Arch arch = RegisterTools.GetArch(regName);

                // by default, the entry.Key is with capitals
                string insertionText = useCapitals ? keyword : keyword.ToLowerInvariant();
                string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                string descriptionStr = _asmDudeTools.Get_Description(keyword);
                string displayText = Truncate(keyword + archStr);

                completions.Add(new CompletionItem
                {
                    Kind = GetCompletionItemKind(AsmTokenType.Register),
                    Label = displayText,
                    InsertText = insertionText,
                    SortText = insertionText,
                    Documentation = descriptionStr
                });
            }
        }

        foreach (string keyword in _asmDudeTools.Get_Keywords())
        {
            AsmTokenType type = _asmDudeTools.Get_Token_Type_Intel(keyword);

            string keyword2 = keyword;
            bool selected = true;

            switch (type)
            {
                case AsmTokenType.Misc:
                    {
                        if (!AsmSignatureTools.Is_Allowed_Misc(keyword, allowedOperands))
                        {
                            selected = false;
                        }
                        break;
                    }
                default:
                    {
                        selected = false;
                        break;
                    }
            }

            if (selected)
            {
                Arch arch = _asmDudeTools.Get_Architecture(keyword);

                // by default, the entry.Key is with capitals
                string insertionText = useCapitals ? keyword2 : keyword2.ToLowerInvariant();
                string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                string descriptionStr = _asmDudeTools.Get_Description(keyword);
                descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                string displayText = Truncate(keyword2 + archStr + descriptionStr);

                completions.Add(new CompletionItem
                {
                    Kind = GetCompletionItemKind(type),
                    Label = displayText,
                    InsertText = insertionText,
                    SortText = insertionText,
                    Documentation = descriptionStr
                });
            }
        }
        return completions;
    }

    private IEnumerable<CompletionItem> Label_Completions(LabelGraph labelGraph, bool useCapitals, bool addSpecialKeywords)
    {
        if (addSpecialKeywords)
        {
            yield return new CompletionItem
            {
                Kind = GetCompletionItemKind(AsmTokenType.Misc),
                Label = "SHORT",
                InsertText = useCapitals ? "SHORT" : "short",
                SortText = "\tSHORT", // use a tab to get on top when sorting
                Documentation = string.Empty
            };
            yield return new CompletionItem
            {
                Kind = GetCompletionItemKind(AsmTokenType.Misc),
                Label = "NEAR",
                InsertText = useCapitals ? "NEAR" : "near",
                SortText = "\tNEAR", // use a tab to get on top when sorting
                Documentation = string.Empty
            };
        }

        AssemblerEnum usedAssembler = _options.Used_Assembler;

        SortedDictionary<string, string> labels = labelGraph.Label_Descriptions;
        foreach (KeyValuePair<string, string> entry in labels)
        {
            string displayTextFull = entry.Key + " - " + entry.Value;
            // TODO: Use Tools.Retrieve_Regular_Label when fully porting LabelGraph functionality
            string insertionText = entry.Key;
            yield return new CompletionItem
            {
                Kind = GetCompletionItemKind(AsmTokenType.Label),
                Label = Truncate(insertionText, 30),
                InsertText = insertionText,
                Documentation = displayTextFull
            };
        }
    }

    private IEnumerable<AsmSignatureInformation> Constrain_Signatures(
        IEnumerable<AsmSignatureInformation> data,
        List<Operand> operands,
        HashSet<Arch> selectedArchitectures)
    {
        foreach (AsmSignatureInformation asmSignatureElement in data)
        {
            bool allowed = true;
            if (!asmSignatureElement.Is_Allowed(selectedArchitectures))
            {
                allowed = false;
            }
            if (allowed)
            {
                for (int i = 0; i < operands.Count; ++i)
                {
                    Operand operand = operands[i];
                    if (operand != null && (operand.IsReg || operand.IsMem || operand.IsImm))
                    {
                        if (!asmSignatureElement.Is_Allowed(operand, i))
                        {
                            allowed = false;
                            break;
                        }
                    }
                }
            }
            if (allowed)
            {
                yield return asmSignatureElement;
            }
        }
    }

    private static (string word, int start, int end) GetWord(int pos, string lineStr)
    {
        (int startPos, int endPos) = FindWordBoundary(pos, lineStr);
        int length = endPos - startPos;

        if (length <= 0)
        {
            return (string.Empty, -1, -1);
        }
        return (lineStr[startPos..endPos], startPos, endPos);
    }

    private static (int start, int end) FindWordBoundary(int position, string lineStr)
    {
        int lineLength = lineStr.Length;
        if (position >= lineLength)
        {
            return (-1, -1);
        }
        if (AsmTools.AsmSourceTools.IsSeparatorChar(lineStr[position]))
        {
            return (-1, -1);
        }

        int startPos = 0;
        int endPos = lineLength;
        char[] lineChars = lineStr.ToCharArray(0, lineLength);

        for (int i = position + 1; i < lineLength; ++i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
            {
                endPos = i;
                break;
            }
        }
        for (int i = position; i >= 0; --i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
            {
                startPos = i + 1;
                break;
            }
        }
        return (startPos, endPos);
    }

    private static string Truncate(string text, int maxLength = MAX_LENGTH_DESCR_TEXT)
    {
        return (text.Length < maxLength) ? text : string.Concat(text.AsSpan(0, maxLength), "...");
    }

    private CompletionItemKind GetCompletionItemKind(AsmTokenType type)
    {
        return type switch
        {
            AsmTokenType.Directive => CompletionItemKind.Value,
            AsmTokenType.Register => CompletionItemKind.Variable,
            AsmTokenType.Misc => CompletionItemKind.Unit,
            AsmTokenType.Label => CompletionItemKind.Reference,
            _ => CompletionItemKind.Text,
        };
    }

    #endregion
}
