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

namespace AsmDude3.SyntaxHighlighting
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Tagging;
    using AsmTools;
    using AsmDude3.Tools;

    /// <summary>
    /// Maps assembly language tokens to classification types (colors and styles).
    /// This classifier obtains raw tokens from AsmTokenTaggerProvider and applies
    /// the appropriate classification tag based on token type and user settings.
    /// </summary>
    public class AsmClassifier : ITagger<ClassificationTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly IClassificationTypeRegistryService _classificationTypeRegistry;
        private readonly ITagAggregator<AsmTokenTag> _aggregator;

        // Pre-allocated classification tags for performance
        private readonly ClassificationTag _mnemonicTag;
        private readonly ClassificationTag _mnemonicOffTag;
        private readonly ClassificationTag _registerTag;
        private readonly ClassificationTag _remarkTag;
        private readonly ClassificationTag _directiveTag;
        private readonly ClassificationTag _jumpTag;
        private readonly ClassificationTag _labelTag;
        private readonly ClassificationTag _labelDefTag;
        private readonly ClassificationTag _constantTag;
        private readonly ClassificationTag _miscTag;
        private readonly ClassificationTag _userDefined1Tag;
        private readonly ClassificationTag _userDefined2Tag;
        private readonly ClassificationTag _userDefined3Tag;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public AsmClassifier(ITextBuffer buffer, IClassificationTypeRegistryService classificationTypeRegistry, IBufferTagAggregatorFactoryService aggregatorFactory)
        {
            this._buffer = buffer;
            this._classificationTypeRegistry = classificationTypeRegistry;
            this._aggregator = aggregatorFactory.CreateTagAggregator<AsmTokenTag>(buffer);

            // Pre-allocate classification tags for performance
            this._mnemonicTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("mnemonic-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._mnemonicOffTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("mnemonicOff-65C24A95-28E9-4141-802D-A40A3FA1081A"));
            this._registerTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("register-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._remarkTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("remark-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._directiveTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("directive-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._jumpTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("jump-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._labelTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("label-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._labelDefTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("labelDef-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._constantTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("constant-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._miscTag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("misc-D74860FA-F0BC-4441-9D76-DF4ECB19CF71"));
            this._userDefined1Tag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("userDefined1-E1A959F6-C591-4B22-ADB3-C5C85BAA0B81"));
            this._userDefined2Tag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("userDefined2-15067A69-A22F-4092-8BEA-FDF985728446"));
            this._userDefined3Tag = new ClassificationTag(this._classificationTypeRegistry.GetClassificationType("userDefined3-80CA80F7-545B-4DA1-B031-8FBA5B9B2126"));
        }

        /// <summary>
        /// Gets classification tags for the specified spans
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0)
            {
                yield break;
            }

            if (!Settings.Default.SyntaxHighlighting_On)
            {
                yield break;
            }

            DateTime startTime = DateTime.Now;

            try
            {
                foreach (var tagSpan in this._aggregator.GetTags(spans))
                {
                    if (tagSpan.Tag == null)
                    {
                        continue;
                    }

                    var tag = tagSpan.Tag;
                    var classificationTag = this.GetClassificationTag(tag.Type);
                    if (classificationTag != null)
                    {
                        // Convert IMappingSpan to SnapshotSpan
                        var snapshotSpans = tagSpan.Span.GetSpans(this._buffer);
                        foreach (var snapshotSpan in snapshotSpans)
                        {
                            yield return new TagSpan<ClassificationTag>(snapshotSpan, classificationTag);
                        }
                    }
                }
            }
            finally
            {
                AsmDudeToolsStatic.Print_Speed_Warning(startTime, "AsmClassifier.GetTags");
            }
        }

        /// <summary>
        /// Maps an AsmTokenType to a pre-allocated ClassificationTag
        /// </summary>
        private ClassificationTag GetClassificationTag(AsmTokenType type)
        {
            switch (type)
            {
                case AsmTokenType.Mnemonic:
                    return this._mnemonicTag;
                case AsmTokenType.MnemonicOff:
                    return this._mnemonicOffTag;
                case AsmTokenType.Register:
                    return this._registerTag;
                case AsmTokenType.Remark:
                    return this._remarkTag;
                case AsmTokenType.Directive:
                    return this._directiveTag;
                case AsmTokenType.Jump:
                    return this._jumpTag;
                case AsmTokenType.Label:
                    return this._labelTag;
                case AsmTokenType.LabelDef:
                    return this._labelDefTag;
                case AsmTokenType.Constant:
                    return this._constantTag;
                case AsmTokenType.Misc:
                    return this._miscTag;
                case AsmTokenType.UserDefined1:
                    return this._userDefined1Tag;
                case AsmTokenType.UserDefined2:
                    return this._userDefined2Tag;
                case AsmTokenType.UserDefined3:
                    return this._userDefined3Tag;
                default:
                    return null;
            }
        }
    }
}
