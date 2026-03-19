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

#nullable enable

using Microsoft.VisualStudio.LanguageServer.Protocol;

using System.Collections.Generic;

namespace AsmDude2LS;

/// <summary>
/// Builds hover responses using VSInternalHover with _vs_rawContent for styled text.
///
/// VS's LSP client only supports PlainText in hover Contents (advertises contentFormat: ["plaintext"]).
/// Markdown is NOT rendered. To get monospace font and colored text, we use the VS-specific
/// _vs_rawContent property with ClassifiedTextElement/ClassifiedTextRun.
///
/// Use "formal language" classification with UseClassificationFont style for monospace rendering.
/// Use "keyword" classification for colored mnemonic names.
///
/// See VSInternalTypes.cs for why clickable links are not possible over LSP.
/// </summary>
public static class HoverBuilder
{
    /// <summary>
    /// Monospace text style: "formal language" + UseClassificationFont = monospace font.
    /// This matches how the old in-process VSIX rendered hover text.
    /// </summary>
    private const ClassifiedTextRunStyle MonospaceStyle = ClassifiedTextRunStyle.UseClassificationFont;

    /// <summary>
    /// Create a hover with a colored keyword followed by monospace description text.
    /// Used for mnemonics/jumps where the keyword should be highlighted.
    /// </summary>
    public static VSInternalHover CreateKeywordHover(string keyword, string description, int line, int startChar, int endChar)
    {
        var runs = new List<ClassifiedTextRun>
        {
            // Keyword in color (uses VS's keyword classification color)
            new(PredefinedClassificationTypeNames.Keyword, keyword, ClassifiedTextRunStyle.Bold),
        };

        if (!string.IsNullOrEmpty(description))
        {
            // Description in monospace font
            runs.Add(new(PredefinedClassificationTypeNames.FormalLanguage, " " + description, MonospaceStyle));
        }

        return BuildHover(runs, line, startChar, endChar);
    }

    /// <summary>
    /// Create a hover with all text in monospace font.
    /// Used for registers, labels, directives, and other non-mnemonic tokens.
    /// </summary>
    public static VSInternalHover CreateMonospaceHover(string content, int line, int startChar, int endChar)
    {
        var runs = new List<ClassifiedTextRun>
        {
            new(PredefinedClassificationTypeNames.FormalLanguage, content, MonospaceStyle),
        };

        return BuildHover(runs, line, startChar, endChar);
    }

    /// <summary>
    /// Create a hover with multiple sections stacked vertically, all in monospace.
    /// Used when hover has description + performance data separated by newlines.
    /// </summary>
    public static VSInternalHover CreateStackedHover(string[] sections, int line, int startChar, int endChar)
    {
        var elements = new List<object>();

        foreach (string section in sections)
        {
            if (string.IsNullOrEmpty(section))
            {
                continue;
            }

            // Split each section into lines for proper stacking
            string[] lines = section.Split('\n');
            foreach (string sectionLine in lines)
            {
                if (string.IsNullOrEmpty(sectionLine))
                {
                    continue;
                }

                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.FormalLanguage, sectionLine, MonospaceStyle)));
            }
        }

        if (elements.Count == 0)
        {
            return CreateMonospaceHover("(no information)", line, startChar, endChar);
        }

        var container = new ContainerElement(ContainerElementStyle.Stacked, [.. elements]);

        return new VSInternalHover
        {
            Contents = null,
            Range = new Range
            {
                Start = new Position(line, startChar),
                End = new Position(line, endChar),
            },
            RawContent = container,
        };
    }

    /// <summary>
    /// Create a mnemonic hover: colored keyword on first line, then stacked monospace description + performance.
    /// </summary>
    public static VSInternalHover CreateMnemonicHover(string keyword, string[] hoverSections, int line, int startChar, int endChar)
    {
        var elements = new List<object>();

        // First element: colored keyword + first section (description) on same line
        if (hoverSections.Length > 0 && !string.IsNullOrEmpty(hoverSections[0]))
        {
            string descr = hoverSections[0];
            // Strip the mnemonic prefix if present — we add the keyword as a separate colored run
            if (descr.StartsWith(keyword, System.StringComparison.OrdinalIgnoreCase))
            {
                descr = descr[keyword.Length..].TrimStart();
            }

            elements.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword, ClassifiedTextRunStyle.Bold),
                new ClassifiedTextRun(PredefinedClassificationTypeNames.FormalLanguage, " " + descr, MonospaceStyle)));
        }
        else
        {
            elements.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword, ClassifiedTextRunStyle.Bold)));
        }

        // Remaining sections (e.g. performance data) as stacked monospace lines
        for (int i = 1; i < hoverSections.Length; i++)
        {
            if (string.IsNullOrEmpty(hoverSections[i]))
            {
                continue;
            }

            string[] lines = hoverSections[i].Split('\n');
            foreach (string sectionLine in lines)
            {
                if (string.IsNullOrEmpty(sectionLine))
                {
                    continue;
                }

                elements.Add(new ClassifiedTextElement(
                    new ClassifiedTextRun(PredefinedClassificationTypeNames.FormalLanguage, sectionLine, MonospaceStyle)));
            }
        }

        var container = new ContainerElement(ContainerElementStyle.Stacked, [.. elements]);

        return new VSInternalHover
        {
            Contents = null,
            Range = new Range
            {
                Start = new Position(line, startChar),
                End = new Position(line, endChar),
            },
            RawContent = container,
        };
    }

    private static VSInternalHover BuildHover(List<ClassifiedTextRun> runs, int line, int startChar, int endChar)
    {
        var textElement = new ClassifiedTextElement([.. runs]);
        var container = new ContainerElement(ContainerElementStyle.Stacked, textElement);

        return new VSInternalHover
        {
            // Contents must be null when RawContent is set — VS renders both if both are present.
            Contents = null,
            Range = new Range
            {
                Start = new Position(line, startChar),
                End = new Position(line, endChar),
            },
            RawContent = container,
        };
    }
}
