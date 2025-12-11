using Microsoft.Extensions.Logging;
using AsmTools;
using AsmDude3.Server.Stores;
using AsmDude3.Server.Models;
using StreamJsonRpc.Protocol;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides signature help (operand format hints) for assembly instructions
/// </summary>
public class SignatureHelpProvider
{
    private readonly ILogger _logger;
    private readonly MnemonicStore _mnemonicStore;
    private readonly AsmLanguageServerOptions _options;

    public SignatureHelpProvider(ILogger logger, MnemonicStore mnemonicStore, AsmLanguageServerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mnemonicStore = mnemonicStore ?? throw new ArgumentNullException(nameof(mnemonicStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Constrain signatures based on operands and architecture
    /// </summary>
    private IEnumerable<AsmSignatureInformation> Constrain_Signatures(
        IEnumerable<AsmSignatureInformation> data,
        List<Operand> operands,
        HashSet<Arch> selectedArchitectures)
    {
#if DEBUG
        bool extraLogging = true;
#else
        bool extraLogging = false;
#endif

        if (extraLogging) _logger.LogInformation("Constrain_Signatures: operands={Operands} data.Count={Count}",
            string.Join(',', operands), data.Count());

        foreach (AsmSignatureInformation asmSignatureElement in data)
        {
            bool allowed = true; // first assume the signature element is allowed; constrain it later

            //1] constrain the signature on architecture
            if (!asmSignatureElement.Is_Allowed(selectedArchitectures))
            {
                if (extraLogging) _logger.LogInformation("Constrain_Signatures: asmSignatureElement {Signature} is not allowed based on arch",
                    asmSignatureElement);
                allowed = false;
            }

            //2] constrain on operands
            if (allowed)
            {
                if ((operands == null) || (operands.Count == 0))
                {
                    // do nothing
                    if (extraLogging) _logger.LogInformation("Constrain_Signatures: operands is null or empty");
                }
                else
                {
                    for (int i = 0; i < operands.Count; ++i)
                    {
                        Operand operand = operands[i];
                        if (operand == null)
                        {
                            _logger.LogError("Constrain_Signatures: somehow got an operand that is null");
                        }
                        else if (operand.IsReg || operand.IsMem || operand.IsImm)
                        {
                            if (extraLogging) _logger.LogInformation("Constrain_Signatures: trying operand={Operand}", operand);
                            if (!asmSignatureElement.Is_Allowed(operand, i))
                            {
                                if (extraLogging) _logger.LogInformation("Constrain_Signatures: asmSignatureElement {Signature} is not allowed based on mnemonic",
                                    asmSignatureElement);
                                allowed = false;
                                break;
                            }
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

    /// <summary>
    /// Provide signature help for a given position in a line
    /// </summary>
    public SignatureHelpInfo? ProvideSignatureHelp(string line, int position, int lineNumber)
    {
        try
        {
#if DEBUG
            bool extraLogging = true;
#else
            bool extraLogging = false;
#endif

            if (!_options.SignatureHelp_On)
            {
                _logger.LogInformation("ProvideSignatureHelp: switched off");
                return null;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            // Allow position to equal line.Length (cursor at end of line)
            if (position > line.Length || position < 0)
            {
                return null;
            }

            // Get text up to cursor
            string lineStr = position == line.Length ? line : line.Substring(0, position);

            if (extraLogging)
            {
                _logger.LogInformation("===========================");
                _logger.LogInformation("ProvideSignatureHelp: lineStr = {LineStr}", lineStr);
            }

            int fileID = 0; // TODO
            (object _, string _, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr, lineNumber, fileID);

            if (extraLogging) _logger.LogInformation("ProvideSignatureHelp: line=\"{Line}\"; lineStr=\"{LineStr}\"; mnemonic={Mnemonic}",
                line, lineStr, mnemonic);

            if (remark.Length > 0)
            {
                _logger.LogInformation("ProvideSignatureHelp: No signature help in a remark");
                return null;
            }

            // we backspace we may backspace into the mnemonic
            if (mnemonic == Mnemonic.NONE)
            {
                return null;
            }

            int mnemonicOffset = lineStr.IndexOf(mnemonic.ToString(), StringComparison.OrdinalIgnoreCase);
            if (mnemonicOffset == -1)
            {
                _logger.LogError("ProvideSignatureHelp: should not happen: investigate");
                return null;
            }

            int argsOffset = mnemonicOffset + mnemonic.ToString().Length + 1;
            int argStrLength = position - argsOffset;
            if (extraLogging) _logger.LogInformation("ProvideSignatureHelp: argsOffset={ArgsOffset}; argStrLength={ArgStrLength}",
                argsOffset, argStrLength);
            if (extraLogging) _logger.LogInformation("ProvideSignatureHelp: current lineNumber: line=\"{Line}\"; mnemonic={Mnemonic}, args={Args}",
                lineStr, mnemonic, string.Join(",", args));

            List<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);
            HashSet<Arch> selectedArchitectures = _options.Get_Arch_Switched_On();

            IEnumerable<AsmSignatureInformation> x = _mnemonicStore.GetSignatures(mnemonic);
            IEnumerable<AsmSignatureInformation> y = Constrain_Signatures(x, operands, selectedArchitectures);
            List<SignatureInformation> z = new();
            foreach (AsmSignatureInformation asmSignatureElement in y)
            {
                if (asmSignatureElement.Operands.Count > 0)
                {
                    if (extraLogging) _logger.LogInformation("ProvideSignatureHelp: adding SignatureInformation: {Label}",
                        asmSignatureElement.SignatureInformation.Label);
                    z.Add(asmSignatureElement.SignatureInformation);
                }
            }
            if (z.Count == 0)
            {
                return null; // no signature help present
            }

            int nCommas = Math.Max(0, operands.Count - 1);

            if (extraLogging) _logger.LogInformation("ProvideSignatureHelp: lineStr=\"{LineStr}\"; pos={Position}; mnemonic={Mnemonic}, nCommas={NCommas}",
                lineStr, position, mnemonic, nCommas);

            return new SignatureHelpInfo
            {
                Signatures = z,
                ActiveSignature = 0,
                ActiveParameter = nCommas
            };
        }
        catch (Exception e)
        {
            _logger.LogError("ProvideSignatureHelp: exception: {Exception}", e);
            return null;
        }
    }
}
