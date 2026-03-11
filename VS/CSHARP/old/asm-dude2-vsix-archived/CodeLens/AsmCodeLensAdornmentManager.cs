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
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using AsmDude2.Tools;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;
    using Newtonsoft.Json.Linq;

    internal sealed class AsmCodeLensAdornmentManager
    {
        internal const string AdornmentLayerName = "AsmCodeLens";
        private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(500);

        private readonly IWpfTextView textView;
        private readonly IAdornmentLayer adornmentLayer;
        private AsmCodeLensLineTransformSource lineTransformSource;
        private readonly DispatcherTimer refreshTimer;

        // definition line -> (label name, reference line numbers)
        private Dictionary<int, (string label, int[] refLines)> codeLensData = new Dictionary<int, (string, int[])>();

        // canvas-coordinate bounds + refLines + TextBlock for each visible adornment
        // Rect uses canvas coordinates (same as Canvas.SetLeft/Top)
        private readonly List<(Rect bounds, int[] refLines, TextBlock block)> activeBlocks
            = new List<(Rect, int[], TextBlock)>();

        private bool needsInitialLoad = true;
        private CancellationTokenSource pendingCts;
        private Popup activePopup;
        private TextBlock hoveredBlock;

        public AsmCodeLensAdornmentManager(IWpfTextView textView)
        {
            this.textView = textView;
            this.adornmentLayer = textView.GetAdornmentLayer(AdornmentLayerName);

            this.textView.Properties.TryGetProperty(typeof(AsmCodeLensLineTransformSource), out this.lineTransformSource);

            this.refreshTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = RefreshDelay,
            };
            this.refreshTimer.Tick += (s, e) =>
            {
                this.refreshTimer.Stop();
                this.RequestCodeLensData();
            };

            this.textView.TextBuffer.Changed += this.OnTextBufferChanged;
            this.textView.LayoutChanged += this.OnLayoutChanged;
            this.textView.Closed += this.OnClosed;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            this.ClosePopup();
            this.refreshTimer.Stop();
            this.pendingCts?.Cancel();
            this.textView.TextBuffer.Changed -= this.OnTextBufferChanged;
            this.textView.LayoutChanged -= this.OnLayoutChanged;
            this.textView.Closed -= this.OnClosed;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            this.ClosePopup();
            this.refreshTimer.Stop();
            this.refreshTimer.Start();
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (this.needsInitialLoad)
            {
                this.needsInitialLoad = false;
                this.RequestCodeLensData();
                return;
            }
            this.UpdateAdornments();
        }

        /// <summary>
        /// Called by the mouse processor on left-click (PreprocessMouseLeftButtonDown).
        /// Returns true if the click hit a CodeLens adornment and was handled.
        /// </summary>
        internal bool TryHandleClick(Point positionInView)
        {
            foreach ((Rect bounds, int[] refLines, _) in this.activeBlocks)
            {
                if (bounds.Contains(positionInView))
                {
                    this.OnCodeLensClicked(refLines);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Called by the mouse processor on move. Handles hover underline.
        /// </summary>
        internal void UpdateHover(Point positionInView)
        {
            TextBlock newHovered = null;
            foreach ((Rect bounds, _, TextBlock block) in this.activeBlocks)
            {
                if (bounds.Contains(positionInView))
                {
                    newHovered = block;
                    break;
                }
            }

            if (newHovered != this.hoveredBlock)
            {
                if (this.hoveredBlock != null)
                {
                    this.hoveredBlock.TextDecorations = null;
                }
                this.hoveredBlock = newHovered;
                if (this.hoveredBlock != null)
                {
                    this.hoveredBlock.TextDecorations = TextDecorations.Underline;
                }
            }
        }

        private string GetDocumentUri()
        {
            if (this.textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document))
            {
                return new Uri(document.FilePath).ToString();
            }
            return null;
        }

        private async void RequestCodeLensData()
        {
            string uri = this.GetDocumentUri();
            if (uri == null)
            {
                return;
            }

            AsmLanguageClient client = AsmLanguageClient.Instance;
            if (client == null)
            {
                return;
            }

            this.pendingCts?.Cancel();
            var cts = new CancellationTokenSource();
            this.pendingCts = cts;

            try
            {
                JArray response = await client.SendCodeLensDataRequestAsync(uri, cts.Token).ConfigureAwait(true);

                if (cts.Token.IsCancellationRequested || this.textView.IsClosed)
                {
                    return;
                }

                var newData = new Dictionary<int, (string label, int[] refLines)>();

                if (response != null)
                {
                    foreach (JToken item in response)
                    {
                        string label = item["label"]?.ToString() ?? item["Label"]?.ToString();
                        int defLine = item["definitionLine"]?.Value<int>() ?? item["DefinitionLine"]?.Value<int>() ?? -1;
                        JArray refArray = (item["referenceLines"] ?? item["ReferenceLines"]) as JArray;

                        if (label != null && defLine >= 0)
                        {
                            int[] refLines;
                            if (refArray != null)
                            {
                                refLines = new int[refArray.Count];
                                for (int i = 0; i < refArray.Count; i++)
                                {
                                    refLines[i] = refArray[i].Value<int>();
                                }
                            }
                            else
                            {
                                refLines = Array.Empty<int>();
                            }
                            newData[defLine] = (label, refLines);
                        }
                    }
                }

                this.codeLensData = newData;

                if (this.lineTransformSource == null)
                {
                    this.textView.Properties.TryGetProperty(typeof(AsmCodeLensLineTransformSource), out this.lineTransformSource);
                }

                this.lineTransformSource?.UpdateLabelLines(new HashSet<int>(newData.Keys));

                this.UpdateAdornments();
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
            catch (Exception ex)
            {
                AsmDudeToolsStatic.Output_WARNING($"AsmCodeLens: LSP request error: {ex.Message}");
            }
        }

        private void UpdateAdornments()
        {
            if (this.adornmentLayer == null || this.textView.IsClosed)
            {
                return;
            }

            // ILineTransformSourceProvider.Create may run after TextViewCreated, so resolve lazily.
            if (this.lineTransformSource == null)
            {
                this.textView.Properties.TryGetProperty(typeof(AsmCodeLensLineTransformSource), out this.lineTransformSource);
            }

            this.adornmentLayer.RemoveAllAdornments();
            this.activeBlocks.Clear();
            this.hoveredBlock = null;

            if (this.textView.TextViewLines == null)
            {
                return;
            }

            Brush foreground = GetCodeLensForeground();

            foreach (ITextViewLine viewLine in this.textView.TextViewLines)
            {
                int lineNumber = viewLine.Snapshot.GetLineNumberFromPosition(viewLine.Start);
                if (!this.codeLensData.TryGetValue(lineNumber, out (string label, int[] refLines) data))
                {
                    continue;
                }

                int refCount = data.refLines.Length;
                string text = refCount == 1 ? "1 reference" : $"{refCount} references";

                double codeLensFontSize = this.lineTransformSource?.CodeLensFontSize
                    ?? viewLine.TextHeight * AsmCodeLensLineTransformSource.CodeLensFontSizeRatio;
                FontFamily codeLensFontFamily = this.lineTransformSource?.CodeLensTypeface?.FontFamily
                    ?? new FontFamily("Consolas");

                int[] capturedRefLines = data.refLines;
                TextBlock textBlock = new TextBlock
                {
                    Text = text,
                    FontSize = codeLensFontSize,
                    Foreground = foreground,
                    FontFamily = codeLensFontFamily,
                    Padding = new Thickness(0, 0, 0, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                };

                // Measure so we know the width for hover hit-testing.
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Size size = textBlock.DesiredSize;

                double left = viewLine.Left;
                double top = viewLine.TextTop - size.Height;

                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);

                ITextSnapshotLine snapshotLine = viewLine.Snapshot.GetLineFromLineNumber(lineNumber);
                SnapshotSpan span = snapshotLine.Extent;

                this.adornmentLayer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    span,
                    null,
                    textBlock,
                    null);

                // Store bounds for hover underline tracking via IMouseProcessor
                this.activeBlocks.Add((new Rect(left, top, size.Width, size.Height), data.refLines, textBlock));
            }
        }

        private void OnCodeLensClicked(int[] refLines)
        {
            this.ClosePopup();

            if (refLines.Length == 0)
            {
                return;
            }

            if (refLines.Length == 1)
            {
                NavigateToLine(refLines[0]);
                return;
            }

            // Multiple references: show a popup list anchored to the text view
            ITextSnapshot snapshot = this.textView.TextSnapshot;
            StackPanel panel = new StackPanel
            {
                Background = GetPopupBackground(),
            };

            foreach (int refLine in refLines)
            {
                string lineText = refLine < snapshot.LineCount
                    ? snapshot.GetLineFromLineNumber(refLine).GetText().Trim()
                    : "";

                TextBlock item = new TextBlock
                {
                    Text = $"  Line {refLine + 1}: {lineText}  ",
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = GetCodeLensForeground(),
                    Padding = new Thickness(4, 3, 4, 3),
                    Cursor = Cursors.Hand,
                };

                Brush hoverBg = GetPopupHoverBackground();
                item.MouseEnter += (s, e) => { item.Background = hoverBg; };
                item.MouseLeave += (s, e) => { item.Background = Brushes.Transparent; };

                int capturedLine = refLine;
                item.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    this.ClosePopup();
                    NavigateToLine(capturedLine);
                };

                panel.Children.Add(item);
            }

            this.activePopup = new Popup
            {
                Child = new Border
                {
                    Child = panel,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2),
                },
                PlacementTarget = this.textView.VisualElement,
                Placement = PlacementMode.MousePoint,
                StaysOpen = false,
                IsOpen = true,
            };
        }

        private void NavigateToLine(int lineNumber)
        {
            try
            {
                ITextSnapshot snapshot = this.textView.TextSnapshot;
                if (lineNumber >= snapshot.LineCount)
                {
                    return;
                }
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                SnapshotPoint point = line.Start;
                this.textView.Caret.MoveTo(point);
                this.textView.Selection.Select(line.Extent, isReversed: false);
                this.textView.ViewScroller.EnsureSpanVisible(line.Extent);
                this.textView.VisualElement.Focus();
            }
            catch (Exception ex)
            {
                AsmDudeToolsStatic.Output_WARNING($"AsmCodeLens: navigate error: {ex.Message}");
            }
        }

        private void ClosePopup()
        {
            if (this.activePopup != null)
            {
                this.activePopup.IsOpen = false;
                this.activePopup = null;
            }
        }

        private Brush GetCodeLensForeground()
        {
            Color bg = GetBackgroundColor();
            double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            return lum < 128
                ? new SolidColorBrush(Color.FromRgb(160, 160, 160))
                : new SolidColorBrush(Color.FromRgb(104, 104, 104));
        }

        private Brush GetPopupBackground()
        {
            Color bg = GetBackgroundColor();
            double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            return lum < 128
                ? new SolidColorBrush(Color.FromRgb(37, 37, 38))
                : new SolidColorBrush(Color.FromRgb(246, 246, 246));
        }

        private Brush GetPopupHoverBackground()
        {
            Color bg = GetBackgroundColor();
            double lum = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            return lum < 128
                ? new SolidColorBrush(Color.FromRgb(62, 62, 64))
                : new SolidColorBrush(Color.FromRgb(220, 220, 224));
        }

        private Color GetBackgroundColor()
        {
            try
            {
                if (this.textView.Background is SolidColorBrush scb)
                {
                    return scb.Color;
                }
            }
            catch { }
            return Colors.White;
        }
    }
}
