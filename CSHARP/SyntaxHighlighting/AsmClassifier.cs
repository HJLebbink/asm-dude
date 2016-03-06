// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude {

    internal static class AsmClassificationDefinition {

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("mnemonic")]
        internal static ClassificationTypeDefinition mnemonic = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("register")]
        internal static ClassificationTypeDefinition register = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("remark")]
        internal static ClassificationTypeDefinition remark = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("directive")]
        internal static ClassificationTypeDefinition directive = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("constant")]
        internal static ClassificationTypeDefinition constant = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("jump")]
        internal static ClassificationTypeDefinition jump = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("label")]
        internal static ClassificationTypeDefinition label = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("misc")]
        internal static ClassificationTypeDefinition misc = null;
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType("asm!")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class AsmClassifierProvider : ITaggerProvider {

        [Export]
        [Name("asm!")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition AsmContentType = null;

        [Export]
        [FileExtension(".asm")]
        [ContentType("asm!")]
        internal static FileExtensionToContentTypeDefinition AsmFileType = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            ITagAggregator<AsmTokenTag> asmTagAggregator = aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);
            return new AsmClassifier(buffer, asmTagAggregator, ClassificationTypeRegistry) as ITagger<T>;
        }
    }

    internal sealed class AsmClassifier : ITagger<ClassificationTag> {
        ITextBuffer _buffer;
        ITagAggregator<AsmTokenTag> _aggregator;
        IDictionary<AsmTokenTypes, IClassificationType> _asmTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal AsmClassifier(ITextBuffer buffer,
                               ITagAggregator<AsmTokenTag> asmTagAggregator,
                               IClassificationTypeRegistryService typeService) {
            _buffer = buffer;
            _aggregator = asmTagAggregator;
            _asmTypes = new Dictionary<AsmTokenTypes, IClassificationType>();
            _asmTypes[AsmTokenTypes.Mnemonic] = typeService.GetClassificationType("mnemonic");
            _asmTypes[AsmTokenTypes.Register] = typeService.GetClassificationType("register");
            _asmTypes[AsmTokenTypes.Remark] = typeService.GetClassificationType("remark");
            _asmTypes[AsmTokenTypes.Directive] = typeService.GetClassificationType("directive");
            _asmTypes[AsmTokenTypes.Constant] = typeService.GetClassificationType("constant");
            _asmTypes[AsmTokenTypes.Jump] = typeService.GetClassificationType("jump");
            _asmTypes[AsmTokenTypes.Label] = typeService.GetClassificationType("label");
            _asmTypes[AsmTokenTypes.Misc] = typeService.GetClassificationType("misc");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged {
            add { }
            remove { }
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (Properties.Settings.Default.SyntaxHighlighting_On) {
                foreach (var tagSpan in _aggregator.GetTags(spans)) {
                    var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    var asmType = _asmTypes[tagSpan.Tag.type];
                    if (asmType == null) {
                        Debug.WriteLine("AsmClassifier:GetTags: asmType is null for " + tagSpan.Tag.type);
                    } else {
                        yield return new TagSpan<ClassificationTag>(tagSpans[0], new ClassificationTag(asmType));
                    }
                }
            }
        }
    }
}
