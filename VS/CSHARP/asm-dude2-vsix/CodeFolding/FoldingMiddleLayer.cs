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

namespace AsmDude2.CodeFolding
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.LanguageServer.Client;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Intercepts LSP folding range requests and returns empty results.
    /// This prevents the LSP client from creating outlining regions that would
    /// conflict with our client-side <see cref="OutliningTagger"/>, which provides
    /// folding regions with hover tooltip support.
    /// </summary>
    internal sealed class FoldingMiddleLayer : ILanguageClientMiddleLayer
    {
        private const string TextDocumentFoldingRangeName = "textDocument/foldingRange";

        public bool CanHandle(string methodName)
        {
            return methodName == TextDocumentFoldingRangeName;
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
                // which provides hover tooltips via OutliningRegionTag.CollapsedHintForm
                return Task.FromResult(JToken.FromObject(new object[0]));
            }

            return sendRequest(methodParam);
        }
    }
}
