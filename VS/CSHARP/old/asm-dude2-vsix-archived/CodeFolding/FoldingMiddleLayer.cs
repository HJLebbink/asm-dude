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

namespace AsmDude2.CodeFolding
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.LanguageServer.Client;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Intercepts LSP requests for folding ranges and hover.
    /// - Folding ranges: returns empty results so our client-side OutliningTagger handles folding.
    /// - Hover: returns JSON null to suppress the default LSP hover QuickInfo.
    ///   Our client-side <see cref="QuickInfo.AsmQuickInfoSource"/> handles hover independently
    ///   via JsonRpc and renders tooltips with a monospace font.
    /// </summary>
    internal sealed class FoldingMiddleLayer : ILanguageClientMiddleLayer
    {
        private const string TextDocumentFoldingRangeName = "textDocument/foldingRange";
        private const string TextDocumentHoverName = "textDocument/hover";

        public bool CanHandle(string methodName)
        {
            return methodName == TextDocumentFoldingRangeName
                || methodName == TextDocumentHoverName;
        }

        public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
        {
            return sendNotification(methodParam);
        }

        public Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
        {
            if (methodName == TextDocumentFoldingRangeName)
            {
                // Return empty array — folding is handled client-side by OutliningTagger
                return Task.FromResult(JToken.FromObject(new object[0]));
            }

            if (methodName == TextDocumentHoverName)
            {
                // Return JSON null to suppress the default LSP hover.
                // Our AsmQuickInfoSource sends its own hover request via JsonRpc.
                return Task.FromResult((JToken)JValue.CreateNull());
            }

            return sendRequest(methodParam);
        }
    }
}
