using AsmSourceTools;

using AsmTools;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Collections.Generic;

namespace LanguageServerLibrary
{
    public class AsmSignatureInformation
    {
        public SignatureInformation SignatureInformation;

        public Mnemonic Mnemonic;
        public Arch[] arch_;
        public IList<IList<AsmSignatureEnum>> operands_;

        /// <summary>Return true if this Signature Element is allowed with the constraints of the provided operand</summary>
        public bool Is_Allowed(Operand op, int operandIndex)
        {
            if (op == null) { return true; }
            if (operandIndex >= this.operands_.Count)
            {
                return false;
            }
            foreach (AsmSignatureEnum operandType in this.operands_[operandIndex])
            {
                if (AsmSignatureTools.Is_Allowed_Operand(op, operandType))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Return true if this Signature Element is allowed in the provided architectures</summary>
        public bool Is_Allowed(ISet<Arch> selectedArchitectures)
        {
            System.Diagnostics.Contracts.Contract.Requires(selectedArchitectures != null);
            foreach (Arch a in this.arch_)
            {
                if (selectedArchitectures.Contains(a))
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSignatureElement: isAllowed: selected architectures=" + ArchTools.ToString(selectedArchitectures) + "; arch = " + ArchTools.ToString(_arch));
                    return true;
                }
            }
            return false;
        }




    }
}
