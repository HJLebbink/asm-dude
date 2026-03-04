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

namespace AsmDude2.QuickInfo
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using AsmDude2.Tools;

    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Adornments;

    internal sealed class AsmQuickInfoSource : IAsyncQuickInfoSource
    {
        private readonly ITextBuffer textBuffer;
        private bool isDisposed;

        public AsmQuickInfoSource(ITextBuffer textBuffer)
        {
            this.textBuffer = textBuffer;
        }

        public async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
        {
            if (this.isDisposed)
            {
                return null;
            }

            // Must be called BEFORE accessing AsmLanguageClient type, which implements
            // ILanguageClientCustomMessage2 and references StreamJsonRpc.JsonRpc.
            AssemblyResolver.EnsureInitialized();

            var triggerPoint = session.GetTriggerPoint(this.textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue)
            {
                return null;
            }

            var client = AsmLanguageClient.Instance;
            if (client == null)
            {
                return null;
            }

            var point = triggerPoint.Value;
            var line = point.GetContainingLine();
            int lineNumber = line.LineNumber;
            int character = point.Position - line.Start.Position;

            string uri = GetDocumentUri(this.textBuffer);
            if (uri == null)
            {
                return null;
            }

            string hoverText = await client.SendHoverRequestAsync(uri, lineNumber, character, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(hoverText))
            {
                return null;
            }


            // Parse "<a href=URL>NAME</a>" embedded in the hover text by the LSP server.
            // The server formats links without quotes: <a href=https://example.com/mov>MOV</a>
            // Build a single ClassifiedTextElement with inline runs so the hyperlink replaces
            // the mnemonic name in-place rather than appearing as a separate stacked element.
            var anchorMatch = Regex.Match(hoverText, @"<a href=([^\s>]+)>([^<]+)</a>");

            ClassifiedTextElement textElement;
            if (anchorMatch.Success)
            {
                string linkUrl = anchorMatch.Groups[1].Value;
                string linkName = anchorMatch.Groups[2].Value;
                string beforeLink = hoverText.Substring(0, anchorMatch.Index);
                string afterLink = hoverText.Substring(anchorMatch.Index + anchorMatch.Length);

                string capturedUrl = linkUrl;
                var runs = new List<ClassifiedTextRun>();
                if (beforeLink.Length > 0)
                {
                    runs.Add(new ClassifiedTextRun("formal language", beforeLink, ClassifiedTextRunStyle.UseClassificationFont));
                }
                runs.Add(new ClassifiedTextRun("keyword", linkName, () => Process.Start(capturedUrl)));
                if (afterLink.Length > 0)
                {
                    runs.Add(new ClassifiedTextRun("formal language", afterLink, ClassifiedTextRunStyle.UseClassificationFont));
                }
                textElement = new ClassifiedTextElement(runs);
            }
            else
            {
                textElement = new ClassifiedTextElement(
                    new ClassifiedTextRun("formal language", hoverText, ClassifiedTextRunStyle.UseClassificationFont));
            }

            // Build tooltip content using VS's native tooltip elements
            var elements = new List<object> { textElement };

            var content = new ContainerElement(ContainerElementStyle.Stacked, elements);

            var currentSnapshot = this.textBuffer.CurrentSnapshot;
            var extent = GetWordExtent(point);
            var applicableSpan = currentSnapshot.CreateTrackingSpan(extent, SpanTrackingMode.EdgeInclusive);

            return new QuickInfoItem(applicableSpan, content);
        }

        private static Span GetWordExtent(SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            string lineText = line.GetText();
            int col = point.Position - line.Start.Position;

            int start = col;
            while (start > 0 && IsWordChar(lineText[start - 1]))
            {
                start--;
            }

            int end = col;
            while (end < lineText.Length && IsWordChar(lineText[end]))
            {
                end++;
            }

            if (start == end)
            {
                return new Span(point.Position, 0);
            }

            return new Span(line.Start.Position + start, end - start);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '%';
        }
        private static string GetDocumentUri(ITextBuffer textBuffer)
        {
            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return new Uri(document.FilePath).ToString();
            }
            return null;
        }

        public void Dispose()
        {
            this.isDisposed = true;
        }
    }
}
