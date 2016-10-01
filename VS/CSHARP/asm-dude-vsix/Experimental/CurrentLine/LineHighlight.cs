using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace Winterdom.VisualStudio.Extensions.Text
{
    public class LineHighlight
    {
        public const string NAME = "Current Line";
        public const string CUR_LINE_TAG = "currentLine";
        private IAdornmentLayer layer;
        private IWpfTextView view;
        private IClassificationFormatMap formatMap;
        private IClassificationType formatType;
        private Brush fillBrush;
        private Pen borderPen;
        private Image currentHighlight = null;

        public LineHighlight(
              IWpfTextView view, IClassificationFormatMap formatMap,
              IClassificationType formatType)
        {
            this.view = view;
            this.formatMap = formatMap;
            this.formatType = formatType;
            layer = view.GetAdornmentLayer(NAME);

            view.Caret.PositionChanged += OnCaretPositionChanged;
            view.ViewportWidthChanged += OnViewportWidthChanged;
            view.LayoutChanged += OnLayoutChanged;
            view.ViewportLeftChanged += OnViewportLeftChanged;
            formatMap.ClassificationFormatMappingChanged += OnClassificationFormatMappingChanged;

            CreateDrawingObjects();
        }

        void OnViewportLeftChanged(object sender, EventArgs e)
        {
            RedrawAdornments();
        }
        void OnViewportWidthChanged(object sender, EventArgs e)
        {
            RedrawAdornments();
        }
        void OnClassificationFormatMappingChanged(object sender, EventArgs e)
        {
            // the user changed something in Fonts and Colors, so recreate our adornments
            this.currentHighlight = null;
            CreateDrawingObjects();
        }
        void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            ITextViewLine newLine = GetLineByPos(e.NewPosition);
            ITextViewLine oldLine = GetLineByPos(e.OldPosition);
            if (newLine != oldLine)
            {
                layer.RemoveAdornmentsByTag(CUR_LINE_TAG);
                this.CreateVisuals(newLine);
            }
        }
        void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            SnapshotPoint caret = view.Caret.Position.BufferPosition;
            foreach (var line in e.NewOrReformattedLines)
            {
                if (line.ContainsBufferPosition(caret))
                {
                    this.currentHighlight = null; // force recalculation
                    this.CreateVisuals(line);
                    break;
                }
            }
        }

        private void CreateDrawingObjects()
        {
            // this gets the color settings configured by the
            // user in Fonts and Colors (or the default in out
            // classification type).
            TextFormattingRunProperties format = formatMap.GetTextProperties(formatType);

            fillBrush = format.BackgroundBrush;
            Brush penBrush = format.ForegroundBrush;
            borderPen = new Pen(penBrush, 0.5);
            borderPen.Freeze();
            RedrawAdornments();
        }
        private void RedrawAdornments()
        {
            if (view.TextViewLines != null)
            {
                if (currentHighlight != null)
                {
                    layer.RemoveAdornment(currentHighlight);
                }
                this.currentHighlight = null; // force redraw
                var caret = view.Caret.Position;
                ITextViewLine line = GetLineByPos(caret);
                this.CreateVisuals(line);
            }
        }
        private ITextViewLine GetLineByPos(CaretPosition pos)
        {
            return view.GetTextViewLineContainingBufferPosition(pos.BufferPosition);
        }
        private void CreateVisuals(ITextViewLine line)
        {
            IWpfTextViewLineCollection textViewLines = view.TextViewLines;
            if (textViewLines == null)
                return; // not ready yet.
            SnapshotSpan span = line.Extent;
            Rect rc = new Rect(
               new Point(line.Left, line.Top),
               new Point(Math.Max(view.ViewportRight - 2, line.Right), line.Bottom)
            );

            if (NeedsNewImage(rc))
            {
                Geometry g = new RectangleGeometry(rc, 1.0, 1.0);
                GeometryDrawing drawing = new GeometryDrawing(fillBrush, borderPen, g);
                drawing.Freeze();
                DrawingImage drawingImage = new DrawingImage(drawing);
                drawingImage.Freeze();
                Image image = new Image();
                // work around WPF rounding bug
                image.UseLayoutRounding = false;
                image.Source = drawingImage;
                currentHighlight = image;
            }

            //Align the image with the top of the bounds of the text geometry
            Canvas.SetLeft(currentHighlight, rc.Left);
            Canvas.SetTop(currentHighlight, rc.Top);

            layer.AddAdornment(
               AdornmentPositioningBehavior.TextRelative, span,
               CUR_LINE_TAG, currentHighlight, null
            );
        }
        private bool NeedsNewImage(Rect rc)
        {
            if (currentHighlight == null)
                return true;
            if (AreClose(currentHighlight.Width, rc.Width))
                return true;
            return AreClose(currentHighlight.Height, rc.Height);
        }
        private bool AreClose(double d1, double d2)
        {
            double diff = d1 - d2;
            return Math.Abs(diff) < 0.1;
        }
    }
}
