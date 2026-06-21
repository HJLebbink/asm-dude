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

namespace AsmTools;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Fills the operand placeholders in an instruction <b>description</b> from the actual operands of a line.
/// There is NO separate template field — a description may simply contain <c>{0}</c>, <c>{1}</c>, … where
/// <c>{0}</c> is the first operand, <c>{1}</c> the second, etc. (authored in the GENERAL rows of the
/// instruction data). For example the description "Move {1} into {0}" rendered against <c>mov rax, rbx</c>
/// becomes "Move rbx into rax". A description with no placeholders passes through unchanged.
/// </summary>
public static class InstructionDescription
{
    /// <param name="description">The (possibly placeholder-bearing) description text.</param>
    /// <param name="operands">Operands in source order ({0}=first). May be empty (e.g. completion has none).</param>
    /// <returns>
    /// <paramref name="description"/> with each <c>{i}</c> replaced by <c>operands[i]</c>. A placeholder
    /// whose index isn't present is rendered as a generic <c>op{i+1}</c>, so the text stays readable when
    /// there are no concrete operands (rather than leaking a literal "{1}").
    /// </returns>
    public static string Render(string description, IReadOnlyList<string> operands)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(operands);

        if (description.IndexOf('{', StringComparison.Ordinal) < 0)
        {
            return description; // fast path: no placeholders (the common case)
        }

        var sb = new StringBuilder(description.Length + 16);
        for (int i = 0; i < description.Length; i++)
        {
            char c = description[i];
            if (c == '{')
            {
                int close = description.IndexOf('}', i + 1);
                if (close > i && int.TryParse(description.AsSpan(i + 1, close - i - 1), out int idx) && idx >= 0)
                {
                    sb.Append(idx < operands.Count ? operands[idx] : $"op{idx + 1}");
                    i = close;
                    continue;
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
