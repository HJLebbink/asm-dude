using AsmTools;
using AsmDude3.Server.Stores;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides hover information (documentation) for assembly language
/// Implements hover functionality matching AsmDude2's behavior
/// </summary>
public class HoverProvider
{
    private readonly ILogger _logger;
    private readonly AsmDude2Tools _asmDudeTools;
    private readonly MnemonicStore _mnemonicStore;
    private readonly PerformanceStore _performanceStore;
    private readonly AsmLanguageServerOptions _options;
    private const int MaxNumberOfCharsInToolTips = 150;

    public HoverProvider(ILogger logger, AsmDude2Tools asmDudeTools, MnemonicStore mnemonicStore, PerformanceStore performanceStore, AsmLanguageServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _asmDudeTools = asmDudeTools ?? throw new ArgumentNullException(nameof(asmDudeTools));
        _mnemonicStore = mnemonicStore ?? throw new ArgumentNullException(nameof(mnemonicStore));
        _performanceStore = performanceStore ?? throw new ArgumentNullException(nameof(performanceStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Provide hover information for a given position in a line
    /// Returns HoverInfo for internal use - will be converted to VSInternalHover or Hover by LanguageServer
    /// </summary>
    public HoverInfo? ProvideHover(string line, int position)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (position < 0 || position >= line.Length || string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        // Find the word at the cursor position
        var (keyword, startPos, endPos) = GetWord(position, line);
        if (string.IsNullOrEmpty(keyword))
        {
            return null;
        }

        string keywordUppercase = keyword.ToUpperInvariant();
        _logger.LogDebug("HoverProvider: keyword={Keyword} at position {Position}", keyword, position);

        // Determine the token type and provide hover content
        var asmTokenType = GetAsmTokenType(keywordUppercase);

        string? contents = null;
        string? url = null;
        string? keywordForLink = null;

        switch (asmTokenType)
        {
            case AsmTokenType.Mnemonic:
            case AsmTokenType.Jump:
                contents = GetMnemonicHover(keywordUppercase);
                // Get URL for clickable hyperlink
                Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keywordUppercase, true);
                if (mnemonic != Mnemonic.NONE)
                {
                    url = _mnemonicStore.GetHtmlRef(mnemonic);
                    keywordForLink = mnemonic.ToString();
                }
                break;
            case AsmTokenType.Register:
                contents = GetRegisterHover(keywordUppercase);
                break;
            case AsmTokenType.UNKNOWN:
                contents = GetUnknownKeywordHover(keywordUppercase);
                break;
        }

        if (contents != null)
        {
            return new HoverInfo
            {
                Contents = contents,
                Range = new HoverRange { StartChar = startPos, EndChar = endPos },
                Url = url,
                Keyword = keywordForLink
            };
        }

        return null;
    }

    /// <summary>
    /// Get the word at the current position from the provided string
    /// </summary>
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

    /// <summary>
    /// Find word boundaries at given position
    /// </summary>
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

    /// <summary>
    /// Determine the assembly token type for a keyword
    /// </summary>
    private AsmTokenType GetAsmTokenType(string keywordUppercase)
    {
        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keywordUppercase, true);
        if (mnemonic != Mnemonic.NONE)
        {
            if (AsmTools.AsmSourceTools.IsJump(mnemonic))
            {
                return AsmTokenType.Jump;
            }
            return AsmTokenType.Mnemonic;
        }

        if (RegisterTools.IsRn(keywordUppercase))
        {
            return AsmTokenType.Register;
        }

        // TODO: Add label and constant support

        return AsmTokenType.UNKNOWN;
    }

    /// <summary>
    /// Get hover content for a mnemonic instruction
    /// Matches AsmDude2's GetHover implementation for mnemonics
    /// Includes clickable hyperlink to documentation
    /// </summary>
    private string? GetMnemonicHover(string keywordUppercase)
    {
        Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keywordUppercase, true);
        if (mnemonic == Mnemonic.NONE)
        {
            return null;
        }

        // Get description from MnemonicStore (signature files)
        string description = _mnemonicStore.GetDescription(mnemonic);
        if (string.IsNullOrEmpty(description))
        {
            description = "Assembly instruction";
        }

        // Get architecture information from MnemonicStore
        var archs = _mnemonicStore.GetArch(mnemonic);
        StringBuilder archStr = new StringBuilder();
        if (archs.Any())
        {
            archStr.Append(" [");
            bool first = true;
            foreach (var arch in archs.Take(5)) // Show first 5 architectures
            {
                if (!first) archStr.Append(", ");
                archStr.Append(ArchTools.ToString(arch));
                first = false;
            }
            if (archs.Count() > 5)
            {
                archStr.Append(", ...");
            }
            archStr.Append("]");
        }

        // Add performance information if enabled
        StringBuilder perfStr = new StringBuilder();
        if (_options.PerformanceInfo_On)
        {
            MicroArch selectedArchitectures = _options.Get_MicroArch_Switched_On();
            var performanceItems = _performanceStore.GetPerformance(mnemonic, selectedArchitectures).ToList();

            if (performanceItems.Any())
            {
                perfStr.AppendLine();
                perfStr.AppendLine();
                perfStr.AppendLine("Performance:");
                perfStr.AppendLine("```");
                perfStr.AppendFormat("{0,-14} {1,-7} {2,-9} {3,-20} {4,-9} {5,-11}\n",
                    "Architecture", "µOps", "µOps", "µOps", "", "");
                perfStr.AppendFormat("{0,-14} {1,-7} {2,-9} {3,-20} {4,-9} {5,-11}\n",
                    "", "Fused", "Merged", "Port", "Latency", "Throughput");

                foreach (var item in performanceItems.Take(10)) // Limit to 10 items
                {
                    perfStr.AppendFormat("{0,-14} {1,-7} {2,-9} {3,-20} {4,-9} {5,-11}\n",
                        item.MicroArch.ToString(),
                        item.MuOpsFused,
                        item.MuOpsMerged,
                        item.MuOpsPort,
                        item.Latency,
                        item.Throughput);
                }
                perfStr.AppendLine("```");
            }
        }

        string fullDescription = (description + archStr.ToString() + perfStr.ToString()).Linewrap(MaxNumberOfCharsInToolTips);
        return fullDescription;
    }

    /// <summary>
    /// Get hover content for a register
    /// Matches AsmDude2's GetHover implementation for registers
    /// </summary>
    private string? GetRegisterHover(string keywordUppercase)
    {
        // Remove AT&T syntax % prefix if present
        if (keywordUppercase.StartsWith("%", StringComparison.Ordinal))
        {
            keywordUppercase = keywordUppercase.Substring(1);
        }

        Rn reg = RegisterTools.ParseRn(keywordUppercase, true);
        if (reg == Rn.NOREG)
        {
            return null;
        }

        string regStr = reg.ToString();
        Arch arch = RegisterTools.GetArch(reg);
        string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : $" [{ArchTools.ToString(arch)}] ";

        // Get description from AsmDudeTools XML data
        string description = _asmDudeTools.Get_Description(regStr);
        if (string.IsNullOrEmpty(description))
        {
            description = string.Empty;
        }

        if (regStr.Length > (MaxNumberOfCharsInToolTips / 2))
        {
            description = "\n" + description;
        }

        string fullDescription = (archStr + description).Linewrap(MaxNumberOfCharsInToolTips);
        return $"Register {regStr}: {fullDescription}";
    }

    /// <summary>
    /// Get hover content for unknown keywords (directives, misc keywords, etc.)
    /// Matches AsmDude2's GetHover implementation for unknown types
    /// </summary>
    private string? GetUnknownKeywordHover(string keywordUppercase)
    {
        string description = _asmDudeTools.Get_Description(keywordUppercase);
        if (string.IsNullOrEmpty(description))
        {
            return null;
        }

        if (keywordUppercase.Length > (MaxNumberOfCharsInToolTips / 2))
        {
            description = "\n" + description;
        }

        description = description.Linewrap(MaxNumberOfCharsInToolTips);
        return $"Keyword {keywordUppercase}: {description}";
    }
}
