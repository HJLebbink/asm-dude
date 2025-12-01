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

namespace AsmDude
{
    using System;
    using System.Collections.Generic;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Tagging;

    internal sealed class AsmClassifier : ITagger<ClassificationTag>
    {
        private readonly ITextBuffer buffer_;
        private readonly ITagAggregator<AsmTokenTag> aggregator_;

        private readonly ClassificationTag mnemonic_;
        private readonly ClassificationTag mnemonicOff_;
        private readonly ClassificationTag register_;
        private readonly ClassificationTag remark_;
        private readonly ClassificationTag directive_;
        private readonly ClassificationTag constant_;
        private readonly ClassificationTag jump_;
        private readonly ClassificationTag label_;
        private readonly ClassificationTag labelDef_;
        private readonly ClassificationTag misc_;
        private readonly ClassificationTag userDefined1_;
        private readonly ClassificationTag userDefined2_;
        private readonly ClassificationTag userDefined3_;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal AsmClassifier(
                ITextBuffer buffer,
                ITagAggregator<AsmTokenTag> asmTagAggregator,
                IClassificationTypeRegistryService typeService)
        {
            this.buffer_ = buffer ?? throw new ArgumentNullException(nameof(buffer));
            this.aggregator_ = asmTagAggregator ?? throw new ArgumentNullException(nameof(asmTagAggregator));

            this.mnemonic_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic));
            this.mnemonicOff_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.MnemonicOff));
            this.register_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Register));
            this.remark_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Remark));
            this.directive_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Directive));
            this.constant_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Constant));
            this.jump_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Jump));
            this.label_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Label));
            this.labelDef_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.LabelDef));
            this.misc_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Misc));
            this.userDefined1_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.UserDefined1));
            this.userDefined2_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.UserDefined2));
            this.userDefined3_ = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.UserDefined3));
        }

        event EventHandler<SnapshotSpanEventArgs> ITagger<ClassificationTag>.TagsChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (Settings.Default.SyntaxHighlighting_On)
            {
                if (spans.Count == 0)
                { //there is no content in the buffer
                    yield break;
                }
                DateTime time1 = DateTime.Now;
                ITextSnapshot snapshot = spans[0].Snapshot;

                foreach (IMappingTagSpan<AsmTokenTag> tagSpan in this.aggregator_.GetTags(spans))
                {
                    NormalizedSnapshotSpanCollection tagSpans = tagSpan.Span.GetSpans(snapshot);
                    switch (tagSpan.Tag.Type)
                    {
                        case AsmTokenType.Mnemonic: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.mnemonic_); break;
                        case AsmTokenType.MnemonicOff: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.mnemonicOff_); break;
                        case AsmTokenType.Register: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.register_); break;
                        case AsmTokenType.Remark: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.remark_); break;
                        case AsmTokenType.Directive: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.directive_); break;
                        case AsmTokenType.Constant: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.constant_); break;
                        case AsmTokenType.Jump: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.jump_); break;
                        case AsmTokenType.Label: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.label_); break;
                        case AsmTokenType.LabelDef: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.labelDef_); break;
                        case AsmTokenType.Misc: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.misc_); break;
                        case AsmTokenType.UserDefined1: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.userDefined1_); break;
                        case AsmTokenType.UserDefined2: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.userDefined2_); break;
                        case AsmTokenType.UserDefined3: yield return new TagSpan<ClassificationTag>(tagSpans[0], this.userDefined3_); break;
                        default:
                            break;
                    }
                }
                AsmDudeToolsStatic.Print_Speed_Warning(time1, "Asm Classifier");
            }
        }
    }
}
