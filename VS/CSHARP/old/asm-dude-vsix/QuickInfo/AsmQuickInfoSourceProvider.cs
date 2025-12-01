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

namespace AsmDude.QuickInfo
{
    using System.ComponentModel.Composition;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;


    [Export(typeof(IAsyncQuickInfoSourceProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)]
    [Name("AsmQuickInfoSourceProvider")]
    [Order]
    internal sealed class QuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
    {
        [Import]
        private readonly IBufferTagAggregatorFactoryService aggregatorFactory_ = null;

        [Import]
        private readonly ITextDocumentFactoryService docFactory_ = null;

        [Import]
        private readonly IContentTypeRegistryService contentService_ = null;

        public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:TryCreateQuickInfoSource", this.ToString()));
            AsmQuickInfoSource sc()
            {
                LabelGraph labelGraph = AsmDudeToolsStatic.GetOrCreate_Label_Graph(textBuffer, this.aggregatorFactory_, this.docFactory_, this.contentService_);
                AsmSimulator asmSimulator = AsmSimulator.GetOrCreate_AsmSimulator(textBuffer, this.aggregatorFactory_);
                return new AsmQuickInfoSource(textBuffer, this.aggregatorFactory_, labelGraph, asmSimulator);
            }
            return textBuffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
