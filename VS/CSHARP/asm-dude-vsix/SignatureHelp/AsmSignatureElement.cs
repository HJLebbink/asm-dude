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
using System.Text;

namespace AsmDude.SignatureHelp
{
    public class AsmSignatureElement
    {
        private readonly Mnemonic _mnemonic;
        private readonly IList<IList<AsmSignatureEnum>> _operands;
        private readonly IList<Arch> _arch;

        private string[] _operandStr;
        private readonly string[] _operandDoc;
        private string _doc;

        public AsmSignatureElement(Mnemonic mnem, string operandStr2, string archStr, string operandDoc, string doc)
        {
            this._mnemonic = mnem;
            this._operands = new List<IList<AsmSignatureEnum>>();
            this._arch = new List<Arch>();

            {
                string[] x = operandDoc.Split(' ');
                if (x.Length > 1)
                {
                    this._operandDoc = x[1].Split(',');
                }
                else
                {
                    this._operandDoc = new string[0];
                }
            }
            this._doc = doc;

            this.Operands_Str = operandStr2;
            this.Arch_Str = archStr;
        }

        public static String Make_Doc(IList<AsmSignatureEnum> operandType)
        {
            StringBuilder sb = new StringBuilder();
            foreach (AsmSignatureEnum op in operandType)
            {
                sb.Append(AsmSignatureTools.Get_Doc(op) + " or ");
            }
            sb.Length -= 4;
            return sb.ToString();
        }

        /// <summary>Return true if this Signature Element is allowed with the constraints of the provided operand</summary>
        public bool Is_Allowed(Operand op, int operandIndex)
        {
            if (op == null) { return true; }
            if (operandIndex >= this.Operands.Count)
            {
                return false;
            }
            foreach (AsmSignatureEnum operandType in this.Operands[operandIndex])
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
            foreach (Arch a in this._arch)
            {
                if (selectedArchitectures.Contains(a))
                {
                    //AsmDudeToolsStatic.Output_INFO("AsmSignatureElement: isAllowed: selected architectures=" + ArchTools.ToString(selectedArchitectures) + "; arch = " + ArchTools.ToString(_arch));
                    return true;
                }
            }
            return false;
        }

        public string Documentation { get { return this._doc; } set { this._doc = value; } }
        public string Arch_Str
        {
            get { return ArchTools.ToString(this._arch); }
            set
            {
                this._arch.Clear();
                if (value == "")
                {
                    //this._arch.Add(Arch.ARCH_486);
                }
                else
                {
                    foreach (string arch2 in value.Split(','))
                    {
                        this._arch.Add(ArchTools.ParseArch(arch2));
                    }
                }
            }
        }
        public IList<Arch> Arch { get { return this._arch; } }

        public Mnemonic Mnemonic { get { return this._mnemonic; } }

        public string Operands_Str
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                int nOperands = this._operandStr.Length;
                for (int i = 0; i < nOperands; ++i)
                {
                    sb.Append(this._operandStr[i]);
                    if (i < nOperands - 1) sb.Append(", ");
                }
                return sb.ToString();
            }
            set
            {
                this._operands.Clear();
                this._operandStr = value.Split(',');

                for (int i = 0; i < this._operandStr.Length; ++i)
                {
                    this._operandStr[i] = this._operandStr[i].Trim();
                    if (this._operandStr[i].Length > 0)
                    {
                        //AsmDudeToolsStatic.Output_INFO("SignatureStore:load: operandStr " + operandStr);
                        IList<AsmSignatureEnum> operandList = new List<AsmSignatureEnum>();
                        AsmSignatureEnum[] operandTypes = AsmSignatureTools.Parse_Operand_Type_Enum(this._operandStr[i]);
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
                            this._operands.Add(operandList);
                        }
                    }
                }
            }
        }

        public IList<IList<AsmSignatureEnum>> Operands { get { return this._operands; } }

        public string Get_Operand_Str(int index)
        {
            return this._operandStr[index];
        }

        public string Sigature_Doc()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this._mnemonic);
            sb.Append(" ");
            foreach (string op in this._operandDoc)
            {
                sb.Append(op);
                sb.Append(",");
            }
            sb.Length--;
            return sb.ToString();
        }

        public string Get_Operand_Doc(int index)
        {
            if (index < this._operandDoc.Length)
            {
                return this._operandDoc[index];
            }
            return "";
        }

        public override String ToString()
        {
            return this.Mnemonic.ToString() + " " + this.Operands_Str;
        }
    }
}
