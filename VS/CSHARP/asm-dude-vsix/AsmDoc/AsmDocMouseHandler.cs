// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using Microsoft.VisualStudio.Text.Formatting;
using AsmDude.Tools;
using System.Collections.Generic;

namespace AsmDude.AsmDoc
{
    /// <summary>
    /// Handle ctrl+click on valid elements to send GoToDefinition to the shell.  Also handle mouse moves
    /// (when control is pressed) to highlight references for which GoToDefinition will (likely) be valid.
    /// </summary>
    internal sealed class AsmDocMouseHandler : MouseProcessorBase
    {
        private readonly IWpfTextView _view;
        private readonly CtrlKeyState _state;
        private readonly IClassifier _aggregator;
        private readonly ITextStructureNavigator _navigator;
        private readonly IOleCommandTarget _commandTarget;
        private readonly AsmDudeTools _asmDudeTools;

        public AsmDocMouseHandler(
            IWpfTextView view,
            IOleCommandTarget commandTarget,
            IClassifier aggregator,
            ITextStructureNavigator navigator,
            CtrlKeyState state,
            AsmDudeTools asmDudeTools)
        {
            //AsmDudeToolsStatic.Output_INFO("AsmDocMouseHandler:constructor: file=" + AsmDudeToolsStatic.GetFileName(view.TextBuffer));
            this._view = view;
            this._commandTarget = commandTarget;
            this._state = state;
            this._aggregator = aggregator;
            this._navigator = navigator;
            this._asmDudeTools = asmDudeTools;

            this._state.CtrlKeyStateChanged += (sender, args) =>
            {
                if (Settings.Default.AsmDoc_On)
                {
                    if (this._state.Enabled)
                    {
                        TryHighlightItemUnderMouse(RelativeToView(Mouse.PrimaryDevice.GetPosition(this._view.VisualElement)));
                    }
                    else
                    {
                        Set_Highlight_Span(null);
                    }
                }
            };

            // Some other points to clear the highlight span:
            this._view.LostAggregateFocus += (sender, args) => Set_Highlight_Span(null);
            this._view.VisualElement.MouseLeave += (sender, args) => Set_Highlight_Span(null);

        }

        #region Mouse processor overrides

        // Remember the location of the mouse on left button down, so we only handle left button up
        // if the mouse has stayed in a single location.
        private Point? _mouseDownAnchorPoint;

        public override void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            this._mouseDownAnchorPoint = RelativeToView(e.GetPosition(this._view.VisualElement));
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            if (!Settings.Default.AsmDoc_On) return;

            if (!this._mouseDownAnchorPoint.HasValue && this._state.Enabled && (e.LeftButton == MouseButtonState.Released))
            {
                TryHighlightItemUnderMouse(RelativeToView(e.GetPosition(this._view.VisualElement)));
            }
            else if (this._mouseDownAnchorPoint.HasValue)
            {
                // Check and see if this is a drag; if so, clear out the highlight.
                var currentMousePosition = RelativeToView(e.GetPosition(this._view.VisualElement));
                if (InDragOperation(this._mouseDownAnchorPoint.Value, currentMousePosition))
                {
                    this._mouseDownAnchorPoint = null;
                    Set_Highlight_Span(null);
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
            this._mouseDownAnchorPoint = null;
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            if (!Settings.Default.AsmDoc_On) return;

            try
            {
                if (this._mouseDownAnchorPoint.HasValue && this._state.Enabled)
                {
                    var currentMousePosition = RelativeToView(e.GetPosition(this._view.VisualElement));

                    if (!InDragOperation(this._mouseDownAnchorPoint.Value, currentMousePosition))
                    {
                        this._state.Enabled = false;

                        ITextViewLine line = this._view.TextViewLines.GetTextViewLineContainingYCoordinate(currentMousePosition.Y);
                        SnapshotPoint? bufferPosition = line.GetBufferPositionFromXCoordinate(currentMousePosition.X);
                        string keyword = AsmDudeToolsStatic.Get_Keyword_Str(bufferPosition);
                        if (keyword != null)
                        {
                            this.Dispatch_Goto_Doc(keyword);
                        }
                        Set_Highlight_Span(null);
                        this._view.Selection.Clear();
                        e.Handled = true;
                    }
                }
                this._mouseDownAnchorPoint = null;
            }
            catch (Exception ex)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0} PreprocessMouseUp; e={1}", ToString(), ex.ToString()));
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
            //AsmDudeToolsStatic.Output_INFO("AsmDocMouseHandler:TryHighlightItemUnderMouse: position=" + position);
            if (!Settings.Default.AsmDoc_On) return false;

            bool updated = false;
            try
            {
                var line = this._view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y);
                if (line == null)
                {
                    return false;
                }
                var bufferPosition = line.GetBufferPositionFromXCoordinate(position.X);
                if (!bufferPosition.HasValue)
                {
                    return false;
                }

                // Quick check - if the mouse is still inside the current underline span, we're already set
                var currentSpan = this.CurrentUnderlineSpan;
                if (currentSpan.HasValue && currentSpan.Value.Contains(bufferPosition.Value))
                {
                    updated = true;
                    return true;
                }

                var extent = this._navigator.GetExtentOfWord(bufferPosition.Value);
                if (!extent.IsSignificant)
                {
                    return false;
                }

                //  check for valid classification type.
                foreach (var classification in this._aggregator.GetClassificationSpans(extent.Span))
                {
                    //TODO check if classification is a mnemonic only then check for an url
                    string keyword = classification.Span.GetText();
                    //string type = classification.ClassificationType.Classification.ToLower();
                    string url = this.Get_Url(keyword);
                    //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:TryHighlightItemUnderMouse: keyword={1}; type={2}; url={3}", this.ToString(), keyword, type, url));
                    if ((url != null) && Set_Highlight_Span(classification.Span))
                    {
                        updated = true;
                        return true;
                    }
                }
                // No update occurred, so return false
                return false;
            }
            finally
            {
                if (!updated)
                {
                    Set_Highlight_Span(null);
                }
            }
        }

        private SnapshotSpan? CurrentUnderlineSpan
        {
            get
            {
                var classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(this._view);
                if (classifier != null && classifier.CurrentUnderlineSpan.HasValue)
                {
                    return classifier.CurrentUnderlineSpan.Value.TranslateTo(this._view.TextSnapshot, SpanTrackingMode.EdgeExclusive);
                }
                else
                {
                    return null;
                }
            }
        }

        private bool Set_Highlight_Span(SnapshotSpan? span)
        {
            var classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(this._view);
            if (classifier != null)
            {
                Mouse.OverrideCursor = (span.HasValue) ? Cursors.Hand : null;
                classifier.SetUnderlineSpan(span);
                return true;
            }
            return false;
        }

        private bool Dispatch_Goto_Doc(string keyword)
        {
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:DispatchGoToDoc; keyword=\"{1}\".", this.ToString(), keyword));
            int hr = this.Open_File(keyword);
            return ErrorHandler.Succeeded(hr);
        }

        private string Get_Url(string keyword)
        {
            string reference = this._asmDudeTools.Get_Url(keyword);
            if (reference.Length == 0)
            {
                return null;
            }
            if (reference.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return reference;
            }
            else
            {
                if (true)
                {
                    return "https://github.com/HJLebbink/asm-dude/wiki/" + reference + "#description";
                }
                else
                {
                    return Settings.Default.AsmDoc_url + reference;
                }
            }
        }

        private EnvDTE.Window GetWindow(DTE2 dte2, string url)
        {
            var enumerator = dte2.Windows.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var window = enumerator.Current as EnvDTE.Window;
                if (window.ObjectKind.Equals(EnvDTE.Constants.vsWindowKindWebBrowser))
                {
                    var url2 = VisualStudioWebBrowser.GetWebBrowserWindowUrl(window).ToString();
                    //AsmDudeToolsStatic.Output_INFO("Documentation " + window.Caption + " is open. url=" + url2.ToString());
                    if (url2.Equals(url, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return window;
                    }
                }
            }
            return null;
        }

        private int Open_File(string keyword)
        {
            string url = this.Get_Url(keyword);
            if (url == null)
            { // this situation happens for all keywords that do not have an url specified (such as registers).
                //AsmDudeToolsStatic.Output_INFO(string.Format("INFO: {0}:openFile; url for keyword \"{1}\" is null.", this.ToString(), keyword));
                return 1;
            }
            //AsmDudeToolsStatic.Output_INFO(string.Format("{0}:openFile; url={1}", this.ToString(), url));

            var dte2 = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (dte2 == null)
            {
                AsmDudeToolsStatic.Output_WARNING(string.Format("{0}:openFile; dte2 is null.", ToString()));
                return 1;
            }

            try
            {
                var window = this.GetWindow(dte2, url);
                if (window == null)
                {
                    // vsNavigateOptionsDefault    0   The Web page opens in the currently open browser window. (Default)
                    // vsNavigateOptionsNewWindow  1   The Web page opens in a new browser window.
                    AsmDudeToolsStatic.Output_INFO(string.Format("{0}:openFile; going to open url {1}.", ToString(), url));
                    window = dte2.ItemOperations.Navigate(url, EnvDTE.vsNavigateOptions.vsNavigateOptionsNewWindow);
                    window.Caption = keyword;
//                    VisualStudioWebBrowser.SetTitle(window, keyword);
                }
                else
                {
                    window.Activate();
                }
                return 0;
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format("{0}:openFile; exception={1}", ToString(), e));
                return 2;
            }
        }
        #endregion
    }

    public class VisualStudioWebBrowser : System.Windows.Forms.WebBrowser
    {
        protected VisualStudioWebBrowser(object IWebBrowser2Object)
        {
            this.IWebBrowser2Object = IWebBrowser2Object;
        }

        protected object IWebBrowser2Object { get; set; }

        private static void Evaluate(EnvDTE.Window WindowReference, Action<System.Windows.Forms.WebBrowser> OnEvaluate)
        {
            //Note: Window of EnvDTE.Constants.vsWindowKindWebBrowser type contains an IWebBrowser2 object
            using (System.Threading.ManualResetEvent evt = new System.Threading.ManualResetEvent(false))
            {
                System.Threading.Thread STAThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart((o) =>
                {
                    try
                    {
                        using (VisualStudioWebBrowser Browser = new VisualStudioWebBrowser(o))
                        {
                            try
                            {
                                OnEvaluate.Invoke((System.Windows.Forms.WebBrowser)Browser);
                            }
                            catch { }
                        }
                    }
                    catch { }
                    evt.Set();
                }));
                STAThread.SetApartmentState(System.Threading.ApartmentState.STA);
                STAThread.Start(WindowReference.Object);
                evt.WaitOne();
            }
        }
        public static Uri GetWebBrowserWindowUrl(EnvDTE.Window WindowReference)
        {
            Uri BrowserUrl = new Uri("", UriKind.RelativeOrAbsolute);
            VisualStudioWebBrowser.Evaluate(WindowReference, new Action<System.Windows.Forms.WebBrowser>((wb) =>
            {
                BrowserUrl = wb.Url;
            }));
            return BrowserUrl;
        }
        public static System.Windows.Forms.WebBrowser GetWebBrowser(EnvDTE.Window WindowReference)
        {
            System.Windows.Forms.WebBrowser wb = null;
            VisualStudioWebBrowser.Evaluate(WindowReference, new Action<System.Windows.Forms.WebBrowser>((wb2) =>
            {
                wb = wb2;
            }));
            return wb;
        }
        protected override void AttachInterfaces(object nativeActiveXObject)
        {
            base.AttachInterfaces(this.IWebBrowser2Object);
        }
        protected override void DetachInterfaces()
        {
            base.DetachInterfaces();
            this.IWebBrowser2Object = null;
        }
    }
}
