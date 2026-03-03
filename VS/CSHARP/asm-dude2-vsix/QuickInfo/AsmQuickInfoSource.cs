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

namespace AsmDude2.QuickInfo
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AsmDude2.Tools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;

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

            // Send textDocument/hover request to the LSP server via AsmLanguageClient
            string hoverText = await client.SendHoverRequestAsync(uri, lineNumber, character, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(hoverText))
            {
                return null;
            }

            // Switch to UI thread to create WPF elements
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var textBlock = new TextBlock
            {
                Text = hoverText,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Padding = new Thickness(4),
                TextWrapping = TextWrapping.NoWrap,
            };

            // Use VS environment colors for theme support
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, Microsoft.VisualStudio.PlatformUI.EnvironmentColors.ToolWindowTextBrushKey);

            // Build the applicable span (the word under cursor)
            var currentSnapshot = this.textBuffer.CurrentSnapshot;
            var extent = GetWordExtent(point);
            var applicableSpan = currentSnapshot.CreateTrackingSpan(extent, SpanTrackingMode.EdgeInclusive);

            return new QuickInfoItem(applicableSpan, textBlock);
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
