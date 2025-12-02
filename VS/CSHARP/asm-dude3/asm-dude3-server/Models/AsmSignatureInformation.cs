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
using AsmDude3.Server.Providers;
using AsmSourceTools;

namespace AsmDude3.Server.Models;

public class AsmSignatureInformation
{
    public required SignatureInformation SignatureInformation { get; init; }
    public required Mnemonic Mnemonic { get; init; }
    public required Arch[] Arch { get; init; }
    public required IList<IList<AsmSignatureEnum>> Operands { get; init; }

    /// <summary>Return true if this Signature Element is allowed with the constraints of the provided operand</summary>
    public bool Is_Allowed(Operand op, int operandIndex)
    {
        if (op == null)
        {
            return true;
        }
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
    public bool Is_Allowed(HashSet<Arch> selectedArchitectures)
    {
        ArgumentNullException.ThrowIfNull(selectedArchitectures);
        foreach (Arch a in this.Arch)
        {
            if (selectedArchitectures.Contains(a))
            {
                return true;
            }
        }
        return false;
    }
}
