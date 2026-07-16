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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmDude2LS;

/// <summary>
/// Builds the hover response in whichever form the current client renders best. The markup
/// <see cref="MarkupKind"/> is negotiated from the client's advertised
/// <c>textDocument.hover.contentFormat</c> at initialize (Markdown when offered, PlainText otherwise)
/// — see <c>LanguageServer.HoverMarkupKind</c>.
///
/// <para><b>Markdown clients (VS Code, and Visual Studio 18.9 or later):</b> a standard
/// <see cref="Hover"/> + <see cref="MarkupContent"/>. The description is plain markdown prose. The
/// leading mnemonic becomes a clickable documentation link. The performance section is a bold heading
/// paragraph plus a slim fenced monospace table per microarchitecture
/// (<see cref="PerformanceDisplay.BuildPerformanceMarkdown"/>). This branch must not rely on richer
/// markdown, because the VS hover host renders only a small subset: it does not render GFM pipe tables
/// (the raw <c>|</c> text leaks through as one paragraph), it silently strips raw HTML such as
/// <c>&lt;details&gt;</c> and standalone <c>---</c> rules, it attaches a "Copy Code" button to every
/// fenced code block, and it wraps fenced monospace text at roughly 55 characters, which tears any
/// wider column layout apart mid-row. Any fenced table must therefore stay narrower than that wrap
/// width.</para>
///
/// <para><b>Plaintext clients (Visual Studio before 18.9, advertises <c>contentFormat:["plaintext"]</c>):</b>
/// VS renders plaintext hover in the <i>proportional</i> environment font, so a space-padded table does
/// not line up (the instruction column is wider in pixels than its character count). Instead we return a
/// <see cref="VSInternalHover"/> whose <c>_vs_rawContent</c> stacks one
/// <see cref="ClassifiedTextElement"/> per line, each a <see cref="ClassifiedTextRun"/> classified as
/// <c>"formal language"</c> with <see cref="ClassifiedTextRunStyle.UseClassificationFont"/>. VS draws
/// that in a fixed-pitch font, so the columns align. The doc URL is appended as a plain line, because
/// hover links are not clickable over LSP (<c>NavigationAction</c> is an unserializable delegate; see
/// the file header in VSInternalTypes.cs).</para>
/// </summary>
public static class HoverBuilder
{
    /// <summary>
    /// Build a hover from a set of text sections (description, performance table, sim state, …).
    /// Returns a <see cref="Hover"/> (Markdown clients) or a <see cref="VSInternalHover"/> (Visual
    /// Studio pre-18.9), or <c>null</c> if there is nothing to show.
    /// </summary>
    /// <param name="perfMarkdownSection">
    /// A pre-composed markdown fragment for the performance section
    /// (<see cref="PerformanceDisplay.BuildPerformanceMarkdown"/>), appended verbatim after the
    /// description. Only Markdown clients use it. When non-null, only <paramref name="sections"/>[0] (the
    /// description) is taken from <paramref name="sections"/>; any further sections are the plaintext
    /// client's representation of the same data and would duplicate it. Pass <c>null</c> when the hover
    /// has no performance section (registers, labels, constants), so all of <paramref name="sections"/>
    /// is used as-is.
    /// </param>
    /// <param name="linkText">
    /// The exact leading substring of <paramref name="sections"/>[0] (typically the mnemonic name) to turn
    /// into the documentation link on Markdown clients. Ignored when it does not match the start of the
    /// text or when <paramref name="docUrl"/> is absent.
    /// </param>
    public static object? CreateHover(
        MarkupKind kind, string[] sections, int line, int startChar, int endChar,
        string? docUrl = null, string? perfMarkdownSection = null, string? linkText = null)
    {
        string body = string.Join(
            "\n",
            sections.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.TrimEnd('\r', '\n')));

        if (string.IsNullOrWhiteSpace(body) && string.IsNullOrEmpty(perfMarkdownSection))
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
            // With a perf section present, only sections[0] (the description) is used; the remaining
            // sections carry the plaintext client's rendering of the same data (see the param doc).
            string description = (perfMarkdownSection != null && sections.Length > 0)
                ? sections[0].TrimEnd('\r', '\n')
                : body;

            bool linkified = !string.IsNullOrEmpty(docUrl) && !string.IsNullOrEmpty(linkText)
                && description.StartsWith(linkText, StringComparison.Ordinal);

            var sb = new StringBuilder();
            if (linkified)
            {
                sb.Append('[').Append(linkText).Append("](").Append(docUrl).Append(')')
                  .Append(description, linkText!.Length, description.Length - linkText.Length);
            }
            else
            {
                sb.Append(description);
            }

            if (!string.IsNullOrEmpty(perfMarkdownSection))
            {
                sb.Append(perfMarkdownSection);
            }

            // The doc link must always be reachable: when the leading text could not be linkified,
            // fall back to a separate trailing link line.
            if (!linkified && !string.IsNullOrEmpty(docUrl))
            {
                sb.Append("\n\n[Documentation](").Append(docUrl).Append(')');
            }

            return new Hover
            {
                Contents = new MarkupContent { Kind = kind, Value = sb.ToString() },
                Range = range,
            };
        }

        // Visual Studio pre-18.9 (plaintext): emit monospace rich content so the table columns line up.
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
