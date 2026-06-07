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

using System.Linq;
using System.Text;

using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS;

/// <summary>
/// Builds a standard LSP <see cref="Hover"/> with <see cref="MarkupContent"/> that renders in every
/// client. The markup <see cref="MarkupKind"/> is negotiated from the client's advertised
/// <c>textDocument.hover.contentFormat</c> at initialize (Markdown when offered, PlainText otherwise)
/// — see <c>LanguageServer.HoverMarkupKind</c>.
///
/// For Markdown the body is wrapped in a <c>```text</c> fence (so the aligned performance table keeps
/// its monospace columns) and a clickable `[Documentation](url)` link is appended. Markdown links —
/// unlike the old `_vs_rawContent` NavigationAction — DO serialize over LSP, so the link is clickable
/// in markdown-rendering clients (e.g. VS Code).
///
/// IMPORTANT: Visual Studio's LSP client advertises <c>contentFormat:["plaintext"]</c> for hover
/// (confirmed in the server log), so VS gets the PlainText branch: the body as-is with the URL on its
/// own line — NOT a clickable link. Markdown (and the clickable link) only applies to clients that
/// advertise Markdown.
/// </summary>
public static class HoverBuilder
{
    /// <summary>
    /// Build a hover from a set of text sections (description, performance table, sim state, …).
    /// Returns <c>null</c> only if there is nothing to show.
    /// </summary>
    public static Hover? CreateHover(
        MarkupKind kind, string[] sections, int line, int startChar, int endChar, string? docUrl = null)
    {
        string body = string.Join(
            "\n",
            sections.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.TrimEnd('\r', '\n')));

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var sb = new StringBuilder();
        if (kind == MarkupKind.Markdown)
        {
            sb.Append("```text\n").Append(body).Append("\n```");
            if (!string.IsNullOrEmpty(docUrl))
            {
                sb.Append("\n\n[Documentation](").Append(docUrl).Append(')');
            }
        }
        else
        {
            sb.Append(body);
            if (!string.IsNullOrEmpty(docUrl))
            {
                sb.Append('\n').Append(docUrl);
            }
        }

        return new Hover
        {
            Contents = new MarkupContent { Kind = kind, Value = sb.ToString() },
            Range = new Range
            {
                Start = new Position(line, startChar),
                End = new Position(line, endChar),
            },
        };
    }
}
