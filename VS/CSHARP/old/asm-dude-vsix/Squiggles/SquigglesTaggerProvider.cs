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

namespace AsmDude.Squiggles
{
    using System.ComponentModel.Composition;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Export a <see cref="IViewTaggerProvider"/>
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TagType(typeof(ErrorTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class SquigglesTaggerProvider : IViewTaggerProvider
    {
        [Import]
        private readonly IBufferTagAggregatorFactoryService aggregatorFactory_ = null;

        [Import]
        private readonly ITextDocumentFactoryService docFactory_ = null;

        [Import]
        private readonly IContentTypeRegistryService contentService_ = null;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            ITagger<T> sc()
            {
                LabelGraph labelGraph = AsmDudeToolsStatic.GetOrCreate_Label_Graph(buffer, this.aggregatorFactory_, this.docFactory_, this.contentService_);
                AsmSimulator asmSimulator = AsmSimulator.GetOrCreate_AsmSimulator(buffer, this.aggregatorFactory_);
                return new SquigglesTagger(buffer, this.aggregatorFactory_, labelGraph, asmSimulator) as ITagger<T>;
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
