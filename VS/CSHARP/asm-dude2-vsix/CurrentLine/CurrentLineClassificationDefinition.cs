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

namespace AsmDude2.CurrentLine
{
    using System.ComponentModel.Composition;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Classification type definition for current line highlighting.
    /// Users can customize the colors in Tools > Options > Fonts and Colors.
    /// </summary>
    internal static class CurrentLineClassificationDefinition
    {
        public const string CurrentLineName = "AsmDude Current Line";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(CurrentLineName)]
        internal static ClassificationTypeDefinition CurrentLineClassificationType = null;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = CurrentLineName)]
        [Name(CurrentLineName)]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        internal sealed class CurrentLineFormatDefinition : ClassificationFormatDefinition
        {
            public CurrentLineFormatDefinition()
            {
                this.DisplayName = "AsmDude - Current Line Highlight";
                // Light yellow background for the current line
                this.BackgroundColor = Color.FromArgb(30, 255, 255, 0);
                this.ForegroundColor = Colors.Transparent;
            }
        }
    }
}
