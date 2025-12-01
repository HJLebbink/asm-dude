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

namespace AsmDude.SignatureHelp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using AsmDude.Tools;
    using AsmTools;

    public class AsmSignatureElement
    {
        #region Fields
        private readonly Mnemonic mnemonic_;
        private readonly IList<IList<AsmSignatureEnum>> operands_;
        private readonly IList<Arch> arch_;
        private readonly bool reversed_Signature_;

        private string[] operandStr_;
        private readonly string[] operandDoc_;
        private string doc_;
        #endregion

        public AsmSignatureElement(Mnemonic mnem, string operandStr2, string archStr, string operandDoc, string doc)
        {
            Contract.Requires(operandDoc != null);

            this.mnemonic_ = mnem;
            this.operands_ = new List<IList<AsmSignatureEnum>>();
            this.arch_ = new List<Arch>();
            this.reversed_Signature_ = AsmDudeToolsStatic.Used_Assembler == AssemblerEnum.NASM_ATT;
            {
                string[] x = operandDoc.Split(' ');
                if (x.Length > 1)
                {
                    this.operandDoc_ = x[1].Split(',');
                }
                else
                {
                    this.operandDoc_ = Array.Empty<string>();
                }
                if (this.reversed_Signature_)
                {
                    Array.Reverse(this.operandDoc_);
                }
            }
            this.doc_ = doc;

            this.Operands_Str = operandStr2;
            this.Arch_Str = archStr;
        }

        public static string Make_Doc(IList<AsmSignatureEnum> operandType)
        {
            Contract.Requires(operandType != null);

            StringBuilder sb = new StringBuilder();
            foreach (AsmSignatureEnum op in operandType)
            {
                sb.Append(AsmSignatureTools.Get_Doc(op) + " or ");
            }
            sb.Length -= 4;
            return sb.ToString();
        }

        #region Getters

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
            Contract.Requires(selectedArchitectures != null);
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

        public string Documentation { get { return this.doc_; } set { this.doc_ = value; } }

        public string Arch_Str
        {
            get { return ArchTools.ToString(this.arch_); }

            set
            {
                this.arch_.Clear();
                if (string.IsNullOrEmpty(value))
                {
                    //this._arch.Add(Arch.ARCH_486);
                }
                else
                {
                    foreach (string arch2 in value.Split(','))
                    {
                        Arch a = ArchTools.ParseArch(arch2, false, true);
                        if (a != AsmTools.Arch.ARCH_NONE)
                        {
                            this.arch_.Add(a);
                        }
                        else
                        {
                            AsmDudeToolsStatic.Output_INFO("Arch_Str: could not parse ARCH string \"" + arch2 + "\".");
                        }
                    }
                }
            }
        }

        public IList<Arch> Arch { get { return this.arch_; } }

        public Mnemonic mnemonic { get { return this.mnemonic_; } }

        public string Operands_Str
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                int nOperands = this.operandStr_.Length;
                for (int i = 0; i < nOperands; ++i)
                {
                    sb.Append(this.operandStr_[i]);
                    if (i < nOperands - 1)
                    {
                        sb.Append(", ");
                    }
                }
                return sb.ToString();
            }

            set
            {
                this.operands_.Clear();

                if (!string.IsNullOrEmpty(value))
                {
                    this.operandStr_ = value.Split(',');
                    if (this.reversed_Signature_)
                    {
                        Array.Reverse(this.operandStr_);
                    }

                    for (int i = 0; i < this.operandStr_.Length; ++i)
                    {
                        this.operandStr_[i] = this.operandStr_[i].Trim();
                        if (this.operandStr_[i].Length > 0)
                        {
                            //AsmDudeToolsStatic.Output_INFO("SignatureStore:load: operandStr " + operandStr);
                            IList<AsmSignatureEnum> operandList = new List<AsmSignatureEnum>();
                            AsmSignatureEnum[] operandTypes = AsmSignatureTools.Parse_Operand_Type_Enum(this.operandStr_[i], false);
                            if ((operandTypes.Length == 1) && ((operandTypes[0] == AsmSignatureEnum.NONE) || (operandTypes[0] == AsmSignatureEnum.UNKNOWN)))
                            {
                                // do nothing
                            }
                            else
                            {
                                foreach (AsmSignatureEnum op in operandTypes)
                                {
                                    operandList.Add(op);
                                }
                            }
                            if (operandList.Count > 0)
                            {
                                this.operands_.Add(operandList);
                            }
                        }
                    }
                }
            }
        }

        public IList<IList<AsmSignatureEnum>> Operands { get { return this.operands_; } }

        public string Get_Operand_Str(int index)
        {
            return this.operandStr_[index];
        }

        public string Sigature_Doc()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.mnemonic_);
            sb.Append(' ');
            foreach (string op in this.operandDoc_)
            {
                sb.Append(op);
                sb.Append(',');
            }
            sb.Length--;
            return sb.ToString();
        }

        public string Get_Operand_Doc(int index)
        {
            if (index < this.operandDoc_.Length)
            {
                return this.operandDoc_[index];
            }
            return string.Empty;
        }

        #endregion

        public override string ToString()
        {
            return this.mnemonic.ToString() + " " + this.Operands_Str;
        }
    }
}
