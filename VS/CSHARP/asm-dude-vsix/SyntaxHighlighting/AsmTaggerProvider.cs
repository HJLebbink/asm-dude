// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace AsmDude.SyntaxHighlighting
{
    [Export(typeof(ITaggerProvider))]
    [Name("AsmDude-AsmTaggerProvider")]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [TagType(typeof(ClassificationTag))]
    internal sealed class AsmTaggerProvider : ITaggerProvider
    {
        [Export]
        [Name("asm!")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition AsmContentType = null;

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType = null;

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_cod = null;

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_inc = null;

        [Import]
        private IClassificationTypeRegistryService _classificationTypeRegistry = null;

        [Import]
        private IBufferTagAggregatorFactoryService _aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            Func<ITagger<T>> sc = delegate () {
                Func<ITagAggregator<AsmTokenTag>> sc2 = delegate () {
                    return _aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
                };
                ITagAggregator<AsmTokenTag> aggregator = buffer.Properties.GetOrCreateSingletonProperty(sc2);

                return new AsmClassifier(buffer, aggregator, _classificationTypeRegistry) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty(sc);
        }
    }
}
