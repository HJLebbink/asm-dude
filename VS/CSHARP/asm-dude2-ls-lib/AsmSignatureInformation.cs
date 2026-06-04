// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

using AsmSourceTools;

using AsmTools;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using System;
using System.Collections.Generic;

namespace AsmDude2LS;

public class AsmSignatureInformation
{
    public required SignatureInformation SignatureInformation;
    public Mnemonic Mnemonic;

    /// <summary>
    /// Architecture requirement in disjunctive normal form (DNF): outer array = OR-alternatives,
    /// inner array = AND-ed members. E.g. [[VL,F],[AVX10]] means (AVX512_VL AND AVX512_F) OR AVX10.
    /// An empty outer array means "no architecture constraint" (always allowed).
    /// </summary>
    public required Arch[][] Arch;
    public required IList<IList<AsmSignatureEnum>> Operands;

    /// <summary>Return true if this Signature Element is allowed with the constraints of the provided operand</summary>
    public bool Is_Allowed(Operand op, int operandIndex)
    {
        if (op == null)
        {
            return true;
        }
        if (operandIndex >= this.Operands.Count)
        {
            //LanguageServer.LogInfo($"AsmSignatureInformation:Is_Allowed operandIndex={operandIndex} >= Operands.Count={this.Operands.Count}");
            return false;
        }
        foreach (AsmSignatureEnum operandType in this.Operands[operandIndex])
        {
            if (AsmSignatureTools.Is_Allowed_Operand(op, operandType))
            {
                return true;
            }
            //LanguageServer.LogInfo($"AsmSignatureInformation:Is_Allowed operandType={operandType} is not allowed for op={op}");
        }
        return false;
    }

    /// <summary>Return true if this Signature Element is allowed in the provided architectures.
    /// DNF semantics: allowed iff ANY OR-group has ALL its AND-members enabled. An empty requirement
    /// (no groups) means the instruction has no architecture gate and is always allowed.</summary>
    public bool Is_Allowed(HashSet<Arch> selectedArchitectures)
    {
        ArgumentNullException.ThrowIfNull(selectedArchitectures);
        if (this.Arch.Length == 0)
        {
            return true; // no architecture constraint (e.g. base/legacy instruction)
        }
        foreach (Arch[] group in this.Arch) // OR over groups
        {
            bool allEnabled = true;
            foreach (Arch a in group) // AND within a group
            {
                if (!selectedArchitectures.Contains(a))
                {
                    allEnabled = false;
                    break;
                }
            }
            if (allEnabled)
            {
                return true;
            }
        }
        return false;
    }
}
