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
using AsmDude.Tools;

namespace AsmDude {

    internal sealed class AsmClassifier : ITagger<ClassificationTag> {

        private readonly ITextBuffer _buffer;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        private readonly ClassificationTag _mnemonic;
        private readonly ClassificationTag _register;
        private readonly ClassificationTag _remark;
        private readonly ClassificationTag _directive;
        private readonly ClassificationTag _constant;
        private readonly ClassificationTag _jump;
        private readonly ClassificationTag _label;
        private readonly ClassificationTag _labelDef;
        private readonly ClassificationTag _misc;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal AsmClassifier(
                ITextBuffer buffer, 
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                IClassificationTypeRegistryService typeService) {
            this._buffer = buffer;
            this._aggregator = asmTagAggregator;

            this._mnemonic = new ClassificationTag(typeService.GetClassificationType("mnemonic"));
            this._register = new ClassificationTag(typeService.GetClassificationType("register"));
            this._remark = new ClassificationTag(typeService.GetClassificationType("remark"));
            this._directive = new ClassificationTag(typeService.GetClassificationType("directive"));
            this._constant = new ClassificationTag(typeService.GetClassificationType("constant"));
            this._jump = new ClassificationTag(typeService.GetClassificationType("jump"));
            this._label = new ClassificationTag(typeService.GetClassificationType("label"));
            this._labelDef = new ClassificationTag(typeService.GetClassificationType("labelDef"));
            this._misc = new ClassificationTag(typeService.GetClassificationType("misc"));
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
                if (spans.Count == 0) {  //there is no content in the buffer
                    yield break;
                }
                DateTime time1 = DateTime.Now;
                foreach (IMappingTagSpan<AsmTokenTag> tagSpan in _aggregator.GetTags(spans)) {
                    NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot);
                    switch (tagSpan.Tag.type) {
                        case AsmTokenType.Mnemonic: yield return new TagSpan<ClassificationTag>(tagSpans[0], _mnemonic); break;
                        case AsmTokenType.Register: yield return new TagSpan<ClassificationTag>(tagSpans[0], _register); break;
                        case AsmTokenType.Remark: yield return new TagSpan<ClassificationTag>(tagSpans[0], _remark); break;
                        case AsmTokenType.Directive: yield return new TagSpan<ClassificationTag>(tagSpans[0], _directive); break;
                        case AsmTokenType.Constant: yield return new TagSpan<ClassificationTag>(tagSpans[0], _constant); break;
                        case AsmTokenType.Jump: yield return new TagSpan<ClassificationTag>(tagSpans[0], _jump); break;
                        case AsmTokenType.Label : yield return new TagSpan<ClassificationTag>(tagSpans[0], _label); break;
                        case AsmTokenType.LabelDef : yield return new TagSpan<ClassificationTag>(tagSpans[0], _labelDef); break;
                        case AsmTokenType.Misc : yield return new TagSpan<ClassificationTag>(tagSpans[0], _misc); break;
                        default:
                            break;
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
