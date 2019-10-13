// The MIT License (MIT)
//
// Copyright (c) 2019 Henk-Jan Lebbink
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

namespace AsmDude.AsmDoc
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using AsmTools;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;
    using Microsoft.VisualStudio.Text.Tagging;

    /// <summary>
    /// Handle ctrl+click on valid elements to send GoToDefinition to the shell.  Also handle mouse moves
    /// (when control is pressed) to highlight references for which GoToDefinition will (likely) be valid.
    /// </summary>
    internal sealed class AsmDocMouseHandler : MouseProcessorBase
    {
        private readonly IWpfTextView _view;
        private readonly CtrlKeyState _state;
        private readonly ITagAggregator<AsmTokenTag> _aggregator2;
        private readonly AsmDudeTools _asmDudeTools;

        public AsmDocMouseHandler(
            IWpfTextView view,
            IBufferTagAggregatorFactoryService aggregatorFactory,
            CtrlKeyState state,
            AsmDudeTools asmDudeTools)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:constructor: file={1}", this.ToString(), AsmDudeToolsStatic.GetFilename(view.TextBuffer)));
            this._view = view ?? throw new ArgumentNullException(nameof(view));
            this._state = state ?? throw new ArgumentNullException(nameof(state));
            this._aggregator2 = AsmDudeToolsStatic.GetOrCreate_Aggregator(view.TextBuffer, aggregatorFactory);
            this._asmDudeTools = asmDudeTools ?? throw new ArgumentNullException(nameof(asmDudeTools));

            this._state.CtrlKeyStateChanged += (sender, args) =>
            {
                if (Settings.Default.AsmDoc_On)
                {
                    if (this._state.Enabled)
                    {
                        this.TryHighlightItemUnderMouse(this.RelativeToView(Mouse.PrimaryDevice.GetPosition(this._view.VisualElement)));
                    }
                    else
                    {
                        this.Set_Highlight_Span(null);
                    }
                }
            };

            // Some other points to clear the highlight span:
            this._view.LostAggregateFocus += (sender, args) =>
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:event: LostAggregateFocus", this.ToString()));
                this.Set_Highlight_Span(null);
            };
            this._view.VisualElement.MouseLeave += (sender, args) =>
            {
                AsmDudeToolsStatic.Output_INFO(string.Format("{0}:event: MouseLeave", this.ToString()));
                this.Set_Highlight_Span(null);
            };
        }

        #region Mouse processor overrides

        // Remember the location of the mouse on left button down, so we only handle left button up
        // if the mouse has stayed in a single location.
        private Point? _mouseDownAnchorPoint;

        public override void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            this._mouseDownAnchorPoint = this.RelativeToView(e.GetPosition(this._view.VisualElement));
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            if (Settings.Default.AsmDoc_On)
            {
                if (!this._mouseDownAnchorPoint.HasValue && this._state.Enabled && (e.LeftButton == MouseButtonState.Released))
                {
                    this.TryHighlightItemUnderMouse(this.RelativeToView(e.GetPosition(this._view.VisualElement)));
                }
                else if (this._mouseDownAnchorPoint.HasValue)
                {
                    // Check and see if this is a drag; if so, clear out the highlight.
                    Point currentMousePosition = this.RelativeToView(e.GetPosition(this._view.VisualElement));
                    if (this.InDragOperation(this._mouseDownAnchorPoint.Value, currentMousePosition))
                    {
                        this._mouseDownAnchorPoint = null;
                        this.Set_Highlight_Span(null);
                    }
                }
            }
        }

        private bool InDragOperation(Point anchorPoint, Point currentPoint)
        {
            // If the mouse up is more than a drag away from the mouse down, this is a drag
            return Math.Abs(anchorPoint.X - currentPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(anchorPoint.Y - currentPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        public override void PreprocessMouseLeave(MouseEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:event: PreprocessMouseLeave; position={1}", this.ToString(), e));
            this._mouseDownAnchorPoint = null;
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            if (Settings.Default.AsmDoc_On)
            {
                try
                {
                    if (this._mouseDownAnchorPoint.HasValue && this._state.Enabled)
                    {
                        Point currentMousePosition = this.RelativeToView(e.GetPosition(this._view.VisualElement));

                        if (!this.InDragOperation(this._mouseDownAnchorPoint.Value, currentMousePosition))
                        {
                            this._state.Enabled = false;

                            ITextViewLine line = this._view.TextViewLines.GetTextViewLineContainingYCoordinate(currentMousePosition.Y);
                            SnapshotPoint? bufferPosition = line.GetBufferPositionFromXCoordinate(currentMousePosition.X);
                            string keyword = AsmDudeToolsStatic.Get_Keyword_Str(bufferPosition);
                            if (keyword != null)
                            {
                                (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(keyword, false);
                                var result = this.Dispatch_Goto_DocAsync(mnemonic).ConfigureAwait(false); // use .ConfigureAwait(false) to signal your intention for continuation.
                            }
                            this.Set_Highlight_Span(null);
                            this._view.Selection.Clear();
                            e.Handled = true;
                        }
                    }
                    this._mouseDownAnchorPoint = null;
                }
                catch (Exception ex)
                {
                    AsmDudeToolsStatic.Output_ERROR(string.Format("{0} PreprocessMouseUp; e={1}", this.ToString(), ex.ToString()));
                }
            }
        }

        #endregion

        #region Private helpers

        private Point RelativeToView(Point position)
        {
            return new Point(position.X + this._view.ViewportLeft, position.Y + this._view.ViewportTop);
        }

        private bool TryHighlightItemUnderMouse(Point position)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format("{0}:event: TryHighlightItemUnderMouse; position={1}", this.ToString(), position));
            if (!Settings.Default.AsmDoc_On)
            {
                return false;
            }

            bool updated = false;
            try
            {
                ITextViewLine line = this._view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y);
                if (line == null)
                {
                    return false;
                }
                SnapshotPoint? bufferPosition = line.GetBufferPositionFromXCoordinate(position.X);
                if (!bufferPosition.HasValue)
                {
                    return false;
                }
                SnapshotPoint triggerPoint = bufferPosition.Value;

                // Quick check - if the mouse is still inside the current underline span, we're already set
                SnapshotSpan? currentSpan = this.CurrentUnderlineSpan;
                if (currentSpan.HasValue && currentSpan.Value.Contains(triggerPoint))
                {
                    updated = true;
                    return true;
                }

                (AsmTokenTag tag, SnapshotSpan? keywordSpan) = AsmDudeToolsStatic.GetAsmTokenTag(this._aggregator2, triggerPoint);
                if (keywordSpan.HasValue)
                {
                    switch (tag.Type)
                    {
                        case AsmTokenType.Mnemonic: // intentional fall through
                        case AsmTokenType.MnemonicOff:
                        case AsmTokenType.Jump:
                            {
                                SnapshotSpan tagSpan = keywordSpan.Value;
                                (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(tagSpan.GetText(), false);
                                string url = this.Get_Url(mnemonic);
                                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:TryHighlightItemUnderMouse: keyword={1}; type={2}; url={3}", this.ToString(), keyword, type, url));
                                if ((url != null) && this.Set_Highlight_Span(keywordSpan))
                                {
                                    updated = true;
                                    return true;
                                }
                                break;
                            }
                        default: break;
                    }
                }
            }
            finally
            {
                if (!updated)
                {
                    this.Set_Highlight_Span(null);
                }
            }
            // No update occurred, so return false
            return false;
        }

        private SnapshotSpan? CurrentUnderlineSpan
        {
            get
            {
                AsmDocUnderlineTagger classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(this._view);
                return ((classifier != null) && classifier.CurrentUnderlineSpan.HasValue)
                    ? classifier.CurrentUnderlineSpan.Value.TranslateTo(this._view.TextSnapshot, SpanTrackingMode.EdgeExclusive)
                    : (SnapshotSpan?)null;
            }
        }

        private bool Set_Highlight_Span(SnapshotSpan? span)
        {
            AsmDocUnderlineTagger classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(this._view);
            if (classifier != null)
            {
                Mouse.OverrideCursor = span.HasValue ? Cursors.Hand : null;
                classifier.SetUnderlineSpan(span);
                return true;
            }
            return false;
        }

        private async Task<bool> Dispatch_Goto_DocAsync(Mnemonic mnemonic)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:DispatchGoToDoc; keyword=\"{1}\".", this.ToString(), keyword));
            int hr = await this.Open_File_Async(mnemonic).ConfigureAwait(false); // use .ConfigureAwait(false) to signal your intention for continuation.
            return ErrorHandler.Succeeded(hr);
        }

        private string Get_Url(Mnemonic mnemonic)
        {
            string reference = this._asmDudeTools.Get_Url(mnemonic);
            if (reference == null)
            {
                return null;
            }

            if (reference.Length == 0)
            {
                return null;
            }

            return reference.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? reference
                : Settings.Default.AsmDoc_Url + reference;
        }

        private async Task<EnvDTE.Window> GetWindowAsync(DTE2 dte2, string url)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            System.Collections.IEnumerator enumerator = dte2.Windows.GetEnumerator();
            while (enumerator.MoveNext())
            {
                EnvDTE.Window window = enumerator.Current as EnvDTE.Window;
                if (window.ObjectKind.Equals(EnvDTE.Constants.vsWindowKindWebBrowser))
                {
                    string url2 = VisualStudioWebBrowser.GetWebBrowserWindowUrl(window).ToString();
                    //AsmDudeToolsStatic.Output_INFO("Documentation " + window.Caption + " is open. url=" + url2.ToString());
                    if (url2.Equals(url, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return window;
                    }
                }
            }
            return null;
        }

        private async Task<int> Open_File_Async(Mnemonic mnemonic)
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            string url = this.Get_Url(mnemonic);
            if (url == null)
            { // this situation happens for all keywords that do not have an url specified (such as registers).
                //AsmDudeToolsStatic.Output_INFO(string.Format("INFO: {0}:openFile; url for keyword \"{1}\" is null.", this.ToString(), keyword));
                return 1;
            }
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Open_File; url={1}", this.ToString(), url));

            DTE2 dte2 = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (dte2 == null)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:Open_File; dte2 is null.", this.ToString()));
                return 1;
            }

            try
            {
                EnvDTE.Window window = await this.GetWindowAsync(dte2, url).ConfigureAwait(false); // use .ConfigureAwait(false) to signal your intention for continuation.
                if (window == null)
                {
                    // vsNavigateOptionsDefault    0   The Web page opens in the currently open browser window. (Default)
                    // vsNavigateOptionsNewWindow  1   The Web page opens in a new browser window.
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:Open_File; going to open url {1}.", this.ToString(), url));
                    window = dte2.ItemOperations.Navigate(url, EnvDTE.vsNavigateOptions.vsNavigateOptionsNewWindow);

                    string[] parts = url.Split('/');
                    string caption = parts[parts.Length - 1];
                    caption = caption.Replace('_', '/');

                    window.Caption = caption;

                    Action action = new Action(() =>
                    {
                        try
                        {
                            ThreadHelper.ThrowIfNotOnUIThread();
                            if (!window.Caption.Equals(caption))
                            {
                                window.Caption = caption;
                            }
                        }
                        catch (Exception e)
                        {
                            AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Open_File; exception={1}", this.ToString(), e));
                        }
                    });
                    DelayAction(100, action);
                    DelayAction(500, action);
                    DelayAction(1000, action);
                    DelayAction(1500, action);
                    DelayAction(3000, action);
                }
                else
                {
                    window.Activate();
                }
                return 0;
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:Open_File; exception={1}", this.ToString(), e));
                return 2;
            }
        }
        #endregion

        private static void DelayAction(int millisecond, Action action)
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += (sender, e) =>
            {
                action.Invoke();
                timer.Stop();
            };
            timer.Interval = TimeSpan.FromMilliseconds(millisecond);
            timer.Start();
        }
    }

    public class VisualStudioWebBrowser : System.Windows.Forms.WebBrowser
    {
        private object iWebBrowser2Object;

        public VisualStudioWebBrowser(object iWebBrowser2Object)
        {
            this.iWebBrowser2Object = iWebBrowser2Object;
        }

        private static void Evaluate(EnvDTE.Window windowReference, Action<System.Windows.Forms.WebBrowser> onEvaluate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Note: Window of EnvDTE.Constants.vsWindowKindWebBrowser type contains an IWebBrowser2 object
            using (System.Threading.ManualResetEvent evt = new System.Threading.ManualResetEvent(false))
            {
                System.Threading.Thread sTAThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart((o) =>
                {
                    try
                    {
                        using (VisualStudioWebBrowser browser = new VisualStudioWebBrowser(o))
                        {
                            try
                            {
                                onEvaluate.Invoke(browser);
                            }
                            catch { }
                        }
                    }
                    catch { }
                    evt.Set();
                }));
                sTAThread.SetApartmentState(System.Threading.ApartmentState.STA);
                sTAThread.Start(windowReference.Object);
                evt.WaitOne();
            }
        }

        public static Uri GetWebBrowserWindowUrl(EnvDTE.Window windowReference)
        {
            Contract.Requires(windowReference != null);
            ThreadHelper.ThrowIfNotOnUIThread();

            Uri browserUrl = new Uri(string.Empty, UriKind.RelativeOrAbsolute);
            Evaluate(windowReference, new Action<System.Windows.Forms.WebBrowser>((wb) => browserUrl = wb.Url));
            return browserUrl;
        }

        protected override void AttachInterfaces(object nativeActiveXObject)
        {
            base.AttachInterfaces(this.iWebBrowser2Object);
        }

        protected override void DetachInterfaces()
        {
            base.DetachInterfaces();
            this.iWebBrowser2Object = null;
        }
    }
}
