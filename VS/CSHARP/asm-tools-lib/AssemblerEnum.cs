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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmTools
{
    using System;

    [Flags]
    public enum AssemblerEnum
    {
        UNKNOWN = 0,
        MASM = 1 << 0,
        NASM_INTEL = 1 << 1,
        NASM_ATT = 1 << 2,
        ALL = NASM_INTEL | NASM_ATT | MASM,
        AUTO_DETECT = 1 << 3,
    }

    public static partial class AsmSourceTools
    {
        public static AssemblerEnum ParseAssembler(string str, bool strIsCapitals)
        {
            if (string.IsNullOrEmpty(str))
            {
                return AssemblerEnum.UNKNOWN;
            }
            AssemblerEnum result = AssemblerEnum.UNKNOWN;

            foreach (string str2 in ToCapitals(str, strIsCapitals).Split(','))
            {
                switch (str2.Trim())
                {
                    case "MASM": result |= AssemblerEnum.MASM; break;
                    case "NASM": result |= AssemblerEnum.NASM_INTEL; break;
                }
            }
            return result;
        }
    }
}
