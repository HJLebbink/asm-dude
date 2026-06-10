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
using System.Linq;
using System.Text;

namespace AsmDude2LS;

/// <summary>
/// Builds the hover response in whichever form the current client renders best. The markup
/// <see cref="MarkupKind"/> is negotiated from the client's advertised
/// <c>textDocument.hover.contentFormat</c> at initialize (Markdown when offered, PlainText otherwise)
/// — see <c>LanguageServer.HoverMarkupKind</c>.
///
/// <para><b>Markdown clients (VS Code):</b> a standard <see cref="Hover"/> + <see cref="MarkupContent"/>
/// whose body is wrapped in a <c>```text</c> fence (so the aligned performance table keeps its
/// monospace columns) with a clickable <c>[Documentation](url)</c> link appended.</para>
///
/// <para><b>Visual Studio (advertises <c>contentFormat:["plaintext"]</c>):</b> VS renders plaintext
/// hover in the <i>proportional</i> environment font, so a space-padded table does NOT line up (the
/// instruction column is wider in pixels than its character count). Instead we return a
/// <see cref="VSInternalHover"/> whose <c>_vs_rawContent</c> stacks one
/// <see cref="ClassifiedTextElement"/> per line, each a <see cref="ClassifiedTextRun"/> classified as
/// <c>"formal language"</c> with <see cref="ClassifiedTextRunStyle.UseClassificationFont"/> — VS draws
/// that in a fixed-pitch font, so the columns align. The doc URL is appended as a plain line (hover
/// links can't be clickable over LSP — <c>NavigationAction</c> is an unserializable delegate; see
/// <see cref="VSInternalTypes"/>/the file header in VSInternalTypes.cs).</para>
/// </summary>
public static class HoverBuilder
{
    /// <summary>
    /// Build a hover from a set of text sections (description, performance table, sim state, …).
    /// Returns a <see cref="Hover"/> (Markdown clients) or a <see cref="VSInternalHover"/> (Visual
    /// Studio), or <c>null</c> if there is nothing to show.
    /// </summary>
    public static object? CreateHover(
        MarkupKind kind, string[] sections, int line, int startChar, int endChar, string? docUrl = null)
    {
        string body = string.Join(
            "\n",
            sections.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.TrimEnd('\r', '\n')));

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var range = new Range
        {
            Start = new Position(line, startChar),
            End = new Position(line, endChar),
        };

        if (kind == MarkupKind.Markdown)
        {
            var sb = new StringBuilder();
            sb.Append("```text\n").Append(body).Append("\n```");
            if (!string.IsNullOrEmpty(docUrl))
            {
                sb.Append("\n\n[Documentation](").Append(docUrl).Append(')');
            }

            return new Hover
            {
                Contents = new MarkupContent { Kind = kind, Value = sb.ToString() },
                Range = range,
            };
        }

        // Visual Studio (plaintext): emit monospace rich content so the table columns line up.
        return new VSInternalHover
        {
            Range = range,
            RawContent = BuildMonospaceRawContent(body, docUrl),
        };
    }

    /// <summary>
    /// Builds a <see cref="ContainerElement"/> that stacks each line of <paramref name="body"/> as its
    /// own monospace <see cref="ClassifiedTextElement"/> (<c>"formal language"</c> +
    /// <see cref="ClassifiedTextRunStyle.UseClassificationFont"/>). Blank lines are rendered as a single
    /// space so VS keeps the vertical gap. The optional <paramref name="docUrl"/> is appended as a final
    /// line (plain, non-clickable — see the class summary).
    /// </summary>
    private static ContainerElement BuildMonospaceRawContent(string body, string? docUrl)
    {
        string[] lines = body.Replace("\r\n", "\n").Split('\n');

        var elements = new List<object>(lines.Length + 1);
        foreach (string raw in lines)
        {
            // VS collapses a truly empty run, so keep a space to preserve blank separator lines.
            string text = raw.Length == 0 ? " " : raw;
            elements.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.FormalLanguage,
                    text,
                    ClassifiedTextRunStyle.UseClassificationFont)));
        }

        if (!string.IsNullOrEmpty(docUrl))
        {
            elements.Add(new ClassifiedTextElement(
                new ClassifiedTextRun(
                    PredefinedClassificationTypeNames.FormalLanguage,
                    docUrl!,
                    ClassifiedTextRunStyle.UseClassificationFont)));
        }

        return new ContainerElement(ContainerElementStyle.Stacked, [.. elements]);
    }
}
