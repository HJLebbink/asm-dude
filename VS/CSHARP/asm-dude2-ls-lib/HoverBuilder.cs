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

#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS
{
    /// <summary>
    /// Builds hover responses with clickable hyperlinks using VSInternalHover
    /// </summary>
    public static class HoverBuilder
    {
        /// <summary>
        /// Create a hover with clickable hyperlink using VSInternalHover.
        /// This uses custom types that serialize to VS-compatible JSON.
        /// </summary>
        /// <param name="keyword">The mnemonic/keyword text to display as link</param>
        /// <param name="url">The URL to navigate to when clicked (can be null)</param>
        /// <param name="description">The description text to show</param>
        /// <param name="line">Line number (0-based)</param>
        /// <param name="startChar">Start character position (0-based)</param>
        /// <param name="endChar">End character position (0-based)</param>
        /// <returns>VSInternalHover with clickable link if URL provided</returns>
        public static VSInternalHover CreateHoverWithLink(string keyword, string? url, string description, int line, int startChar, int endChar)
        {
            var runs = new List<ClassifiedTextRun>();

            // Create hyperlink run if URL exists
            if (!string.IsNullOrEmpty(url))
            {
                runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword, url));
            }
            else
            {
                runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword));
            }

            // Add description
            if (!string.IsNullOrEmpty(description))
            {
                runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, ": " + description));
            }

            // Create classified text element
            var textElement = new ClassifiedTextElement(runs.ToArray());

            // Create container
            var container = new ContainerElement(ContainerElementStyle.Wrapped, textElement);

            // Create VSInternalHover with both standard Contents and RawContent
            var hover = new VSInternalHover
            {
                // Standard LSP content (for non-VS clients)
                Contents = new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = !string.IsNullOrEmpty(url) ? $"[{keyword}]({url}): {description}" : $"{keyword}: {description}"
                },
                // Range of the hovered text
                Range = new Range
                {
                    Start = new Position(line, startChar),
                    End = new Position(line, endChar)
                },
                // VS-specific RawContent with clickable hyperlink
                RawContent = container
            };

            return hover;
        }

        /// <summary>
        /// Create a standard hover without hyperlink
        /// </summary>
        public static Hover CreateStandardHover(string content, int line, int startChar, int endChar)
        {
            // Use MarkupContent instead of deprecated MarkedString
            var markupContent = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = content
            };

            return new Hover
            {
                Contents = markupContent,
                Range = new Range
                {
                    Start = new Position(line, startChar),
                    End = new Position(line, endChar)
                }
            };
        }
    }
}
