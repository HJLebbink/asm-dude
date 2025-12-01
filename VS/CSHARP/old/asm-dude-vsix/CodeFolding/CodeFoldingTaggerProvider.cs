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

namespace AsmDude.CodeFolding
{
    using System.ComponentModel.Composition;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Export a <see cref="ITaggerProvider"/>
    /// </summary>
    [Export(typeof(ITaggerProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TagType(typeof(IOutliningRegionTag))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class CodeFoldingTaggerProvider : ITaggerProvider
    {
        [Import]
        private readonly IBufferTagAggregatorFactoryService aggregatorFactory_ = null;

        /// <summary>
        /// This method is called by VS to generate the tagger
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"> The text view we are creating a tagger for</param>
        /// <returns> Returns a OutliningTagger instance</returns>
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            ITagger<T> sc()
            {
                ITagAggregator<SyntaxHighlighting.AsmTokenTag> aggregator = AsmDudeToolsStatic.GetOrCreate_Aggregator(buffer, this.aggregatorFactory_);
                return new CodeFoldingTagger(buffer, aggregator, AsmDudeTools.Instance.Error_List_Provider) as ITagger<T>;
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
