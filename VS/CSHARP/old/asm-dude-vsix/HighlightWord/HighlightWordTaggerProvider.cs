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

namespace AsmDude.HighlightWord
{
    using System.ComponentModel.Composition;
    using System.Diagnostics.Contracts;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Operations;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IViewTaggerProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [ContentType(AsmDudePackage.DisassemblyContentType)]
    [TagType(typeof(HighlightWordTag))]
    public class HighlightWordTaggerProvider : IViewTaggerProvider
    {
        [Import]
        private readonly ITextSearchService textSearchService_ = null;

        [Import]
        private readonly ITextStructureNavigatorSelectorService textStructureNavigatorSelector_ = null;

        /// <summary>
        /// This method is called by VS to generate the tagger
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="textView"> The text view we are creating a tagger for</param>
        /// <param name="buffer"> The buffer that the tagger will examine for instances of the current word</param>
        /// <returns> Returns a HighlightWordTagger instance</returns>
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            Contract.Requires(buffer != null);

            if (textView == null)
            {
                return null;
            }
            // Only provide highlighting on the top-level buffer
            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            ITagger<T> sc()
            {
                ITextStructureNavigator textStructureNavigator = this.textStructureNavigatorSelector_.GetTextStructureNavigator(buffer);
                return new HighlightWordTagger(textView, buffer, this.textSearchService_, textStructureNavigator) as ITagger<T>;
            }
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
