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
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;

    /// <summary>
    /// Highlights the current line in assembly files with a subtle background color.
    /// </summary>
    internal sealed class CurrentLineHighlight
    {
        public const string AdornmentLayerName = "AsmDudeCurrentLine";
        private const string AdornmentTag = "currentLine";

        private readonly IAdornmentLayer layer;
        private readonly IWpfTextView view;
        private readonly IClassificationFormatMap formatMap;
        private readonly IClassificationType formatType;

        private Brush fillBrush;
        private Pen borderPen;
        private Image currentHighlight;

        public CurrentLineHighlight(
            IWpfTextView view,
            IClassificationFormatMap formatMap,
            IClassificationType formatType)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.formatMap = formatMap ?? throw new ArgumentNullException(nameof(formatMap));
            this.formatType = formatType;

            this.layer = view.GetAdornmentLayer(AdornmentLayerName);

            this.view.Caret.PositionChanged += this.OnCaretPositionChanged;
            this.view.ViewportWidthChanged += this.OnViewportWidthChanged;
            this.view.LayoutChanged += this.OnLayoutChanged;
            this.view.ViewportLeftChanged += this.OnViewportLeftChanged;
            this.formatMap.ClassificationFormatMappingChanged += this.OnClassificationFormatMappingChanged;

            this.CreateDrawingObjects();
        }

        private void OnViewportLeftChanged(object sender, EventArgs e)
        {
            this.RedrawAdornments();
        }

        private void OnViewportWidthChanged(object sender, EventArgs e)
        {
            this.RedrawAdornments();
        }

        private void OnClassificationFormatMappingChanged(object sender, EventArgs e)
        {
            // User changed Fonts and Colors settings, recreate adornments
            this.currentHighlight = null;
            this.CreateDrawingObjects();
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            ITextViewLine newLine = this.GetLineByPos(e.NewPosition);
            ITextViewLine oldLine = this.GetLineByPos(e.OldPosition);

            if (newLine != oldLine)
            {
                this.layer.RemoveAdornmentsByTag(AdornmentTag);
                this.CreateVisuals(newLine);
            }
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            SnapshotPoint caret = this.view.Caret.Position.BufferPosition;

            foreach (var line in e.NewOrReformattedLines)
            {
                if (line.ContainsBufferPosition(caret))
                {
                    this.currentHighlight = null; // Force recalculation
                    this.CreateVisuals(line);
                    break;
                }
            }
        }

        private void CreateDrawingObjects()
        {
            if (this.formatType != null)
            {
                // Get color settings from Fonts and Colors
                TextFormattingRunProperties format = this.formatMap.GetTextProperties(this.formatType);
                this.fillBrush = format.BackgroundBrush;
                Brush penBrush = format.ForegroundBrush;

                // Fallback to default if no brush is configured
                if (this.fillBrush == null)
                {
                    this.fillBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 0));
                    this.fillBrush.Freeze();
                }

                this.borderPen = new Pen(penBrush ?? Brushes.Transparent, 0.5);
                this.borderPen.Freeze();
            }
            else
            {
                // Default colors if classification type not found
                this.fillBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 0));
                this.fillBrush.Freeze();
                this.borderPen = new Pen(Brushes.Transparent, 0);
                this.borderPen.Freeze();
            }

            this.RedrawAdornments();
        }

        private void RedrawAdornments()
        {
            if (this.view.TextViewLines != null)
            {
                if (this.currentHighlight != null)
                {
                    this.layer.RemoveAdornment(this.currentHighlight);
                }
                this.currentHighlight = null; // Force redraw
                var caret = this.view.Caret.Position;
                ITextViewLine line = this.GetLineByPos(caret);
                this.CreateVisuals(line);
            }
        }

        private ITextViewLine GetLineByPos(CaretPosition pos)
        {
            return this.view.GetTextViewLineContainingBufferPosition(pos.BufferPosition);
        }

        private void CreateVisuals(ITextViewLine line)
        {
            if (line == null)
            {
                return;
            }

            IWpfTextViewLineCollection textViewLines = this.view.TextViewLines;
            if (textViewLines == null)
            {
                return; // Not ready yet
            }

            SnapshotSpan span = line.Extent;
            Rect rc = new Rect(
               new Point(line.Left, line.Top),
               new Point(Math.Max(this.view.ViewportRight - 2, line.Right), line.Bottom)
            );

            if (this.NeedsNewImage(rc))
            {
                Geometry g = new RectangleGeometry(rc, 1.0, 1.0);
                GeometryDrawing drawing = new GeometryDrawing(this.fillBrush, this.borderPen, g);
                drawing.Freeze();
                DrawingImage drawingImage = new DrawingImage(drawing);
                drawingImage.Freeze();
                Image image = new Image
                {
                    UseLayoutRounding = false, // Work around WPF rounding bug
                    Source = drawingImage
                };
                this.currentHighlight = image;
            }

            // Align the image with the top of the bounds of the text geometry
            Canvas.SetLeft(this.currentHighlight, rc.Left);
            Canvas.SetTop(this.currentHighlight, rc.Top);

            this.layer.AddAdornment(
               AdornmentPositioningBehavior.TextRelative, span,
               AdornmentTag, this.currentHighlight, null
            );
        }

        private bool NeedsNewImage(Rect rc)
        {
            if (this.currentHighlight == null)
            {
                return true;
            }
            if (!AreClose(this.currentHighlight.Width, rc.Width))
            {
                return true;
            }
            return !AreClose(this.currentHighlight.Height, rc.Height);
        }

        private static bool AreClose(double d1, double d2)
        {
            double diff = d1 - d2;
            return Math.Abs(diff) < 0.1;
        }
    }
}
