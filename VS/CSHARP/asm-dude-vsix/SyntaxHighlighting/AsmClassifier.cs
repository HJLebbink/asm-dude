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


    internal sealed class AsmClassifier : ITagger<ClassificationTag> {

        private ITextBuffer _buffer;
        private ITagAggregator<AsmTokenTag> _aggregator;
        private IDictionary<TokenType, IClassificationType> _asmTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal AsmClassifier(
                ITextBuffer buffer, 
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                IClassificationTypeRegistryService typeService) {
            _buffer = buffer;
            _aggregator = asmTagAggregator;
            _asmTypes = new Dictionary<TokenType, IClassificationType>();
            _asmTypes[TokenType.Mnemonic] = typeService.GetClassificationType("mnemonic");
            _asmTypes[TokenType.Register] = typeService.GetClassificationType("register");
            _asmTypes[TokenType.Remark] = typeService.GetClassificationType("remark");
            _asmTypes[TokenType.Directive] = typeService.GetClassificationType("directive");
            _asmTypes[TokenType.Constant] = typeService.GetClassificationType("constant");
            _asmTypes[TokenType.Jump] = typeService.GetClassificationType("jump");
            _asmTypes[TokenType.Label] = typeService.GetClassificationType("label");
            _asmTypes[TokenType.Misc] = typeService.GetClassificationType("misc");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (Settings.Default.SyntaxHighlighting_On) {
                DateTime time1 = DateTime.Now;

                if (spans.Count == 0) {  //there is no content in the buffer
                    yield break;
                }
                foreach (IMappingTagSpan<AsmTokenTag> tagSpan in _aggregator.GetTags(spans)) {
                    NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    IClassificationType asmType = _asmTypes[tagSpan.Tag.type];
                    if (asmType == null) {
                        Debug.WriteLine("AsmClassifier:GetTags: asmType is null for " + tagSpan.Tag.type);
                    } else {
                        yield return new TagSpan<ClassificationTag>(tagSpans[0], new ClassificationTag(asmType));
                    }
                }
                double elapsedSec = (double)(DateTime.Now.Ticks - time1.Ticks) / 10000000;
                if (elapsedSec > AsmDudePackage.slowWarningThresholdSec) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: SLOW: took {0:F3} seconds to assign classification tags for syntax highlighting.", elapsedSec));
                }
            }
        }
    }
}
