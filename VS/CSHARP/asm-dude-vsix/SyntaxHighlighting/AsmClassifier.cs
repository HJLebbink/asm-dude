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
using System.Diagnostics;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using AsmDude.SyntaxHighlighting;

namespace AsmDude {

    internal sealed class AsmClassifier : ITagger<ClassificationTag> {

        private ITextBuffer _buffer;
        private ITagAggregator<AsmTokenTag> _aggregator;
        private IDictionary<AsmTokenType, IClassificationType> _asmTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal AsmClassifier(
                ITextBuffer buffer, 
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                IClassificationTypeRegistryService typeService) {
            _buffer = buffer;
            _aggregator = asmTagAggregator;
            _asmTypes = new Dictionary<AsmTokenType, IClassificationType>();
            _asmTypes[AsmTokenType.Mnemonic] = typeService.GetClassificationType("mnemonic");
            _asmTypes[AsmTokenType.Register] = typeService.GetClassificationType("register");
            _asmTypes[AsmTokenType.Remark] = typeService.GetClassificationType("remark");
            _asmTypes[AsmTokenType.Directive] = typeService.GetClassificationType("directive");
            _asmTypes[AsmTokenType.Constant] = typeService.GetClassificationType("constant");
            _asmTypes[AsmTokenType.Jump] = typeService.GetClassificationType("jump");
            _asmTypes[AsmTokenType.Label] = typeService.GetClassificationType("label");
            _asmTypes[AsmTokenType.Misc] = typeService.GetClassificationType("misc");
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<ClassificationTag>.TagsChanged {
            add { }
            remove { }
        }

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
