// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmDude2.CodeLens
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;

    internal sealed class AsmCodeLensLineTransformSource : ILineTransformSource
    {
        // CodeLens text is rendered at this fraction of the editor's font size.
        // At VS default Consolas 12pt the editor FontRenderingEmSize ≈ 16px:
        //   16 × 0.70 ≈ 11pt — matches the previous hardcoded value.
        internal const double CodeLensFontSizeRatio = 0.70;

        private readonly IClassificationFormatMap formatMap;

        // Cached metrics derived from the format map. Invalidated when FormatMappingChanged fires.
        private bool cacheValid = false;
        private double cachedTopSpace;
        private double cachedFontSize;
        private Typeface cachedTypeface;

        private HashSet<int> labelDefinitionLines = new HashSet<int>();

        public AsmCodeLensLineTransformSource(IWpfTextView textView, IClassificationFormatMap formatMap)
        {
            this.formatMap = formatMap;
            this.formatMap.ClassificationFormatMappingChanged += this.OnFormatMappingChanged;
            textView.Closed += (s, e) => this.formatMap.ClassificationFormatMappingChanged -= this.OnFormatMappingChanged;
        }

        private void OnFormatMappingChanged(object sender, EventArgs e)
        {
            this.cacheValid = false;
            // VS re-layouts the view after ClassificationFormatMappingChanged,
            // which re-calls GetLineTransform with the updated font metrics.
        }

        private void EnsureCache()
        {
            if (this.cacheValid)
            {
                return;
            }

            var props = this.formatMap.DefaultTextProperties;
            double editorFontSize = props.FontRenderingEmSize;
            this.cachedFontSize = editorFontSize * CodeLensFontSizeRatio;
            this.cachedTypeface = props.Typeface;

            // Use the actual typeface baseline (sTypoAscender / unitsPerEm) so the visible ink
            // sits flush above the label line with no wasted gap.
            if (this.cachedTypeface != null && this.cachedTypeface.TryGetGlyphTypeface(out GlyphTypeface gt))
            {
                this.cachedTopSpace = Math.Ceiling(this.cachedFontSize * gt.Baseline);
            }
            else
            {
                this.cachedTopSpace = Math.Ceiling(this.cachedFontSize * 0.73); // fallback
            }

            this.cacheValid = true;
        }

        // TopSpace is used only by GetLineTransform to reserve blank space above label lines.
        // Font size and typeface are read by AsmCodeLensAdornmentManager to style the TextBlock.
        internal double TopSpace { get { this.EnsureCache(); return this.cachedTopSpace; } }
        internal double CodeLensFontSize { get { this.EnsureCache(); return this.cachedFontSize; } }
        internal Typeface CodeLensTypeface { get { this.EnsureCache(); return this.cachedTypeface; } }

        internal void UpdateLabelLines(HashSet<int> lines)
        {
            this.labelDefinitionLines = lines;
        }

        public LineTransform GetLineTransform(ITextViewLine line, double yPosition, ViewRelativePosition placement)
        {
            int lineNumber = line.Snapshot.GetLineNumberFromPosition(line.Start);
            if (this.labelDefinitionLines.Contains(lineNumber))
            {
                return new LineTransform(this.TopSpace, 0, 1.0);
            }
            return new LineTransform(0, 0, 1.0);
        }
    }
}
