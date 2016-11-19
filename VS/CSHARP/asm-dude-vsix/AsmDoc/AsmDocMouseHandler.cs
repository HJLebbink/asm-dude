// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
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
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using Microsoft.VisualStudio.Text.Formatting;
using AsmDude.Tools;

namespace AsmDude.AsmDoc
{
    [Export(typeof(IKeyProcessorProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    //[ContentType("code")]
    [Name("AsmDoc")]
    [Order(Before = "VisualStudioKeyboardProcessor")]
    internal sealed class AsmDocKeyProcessorProvider : IKeyProcessorProvider
    {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            //AsmDudeToolsStatic.Output("INFO: AsmDocKeyProcessorProvider:GetAssociatedProcessor: file=" + AsmDudeToolsStatic.GetFileName(view.TextBuffer));
            return view.Properties.GetOrCreateSingletonProperty(typeof(AsmDocKeyProcessor), () => new AsmDocKeyProcessor(CtrlKeyState.GetStateForView(view)));
        }
    }

    /// <summary>
    /// The state of the control key for a given view, which is kept up-to-date by a combination of the
    /// key processor and the mouse process
    /// </summary>
    internal sealed class CtrlKeyState
    {
        internal static CtrlKeyState GetStateForView(ITextView view)
        {
            return view.Properties.GetOrCreateSingletonProperty(typeof(CtrlKeyState), () => new CtrlKeyState());
        }

        bool _enabled = false;

        internal bool Enabled {
            get {
                // Check and see if ctrl is down but we missed it somehow.
                bool ctrlDown = (Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                                (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
                if (ctrlDown != _enabled)
                {
                    Enabled = ctrlDown;
                }
                return _enabled;
            }
            set {
                bool oldVal = _enabled;
                _enabled = value;
                if (oldVal != _enabled)
                {
                    var temp = CtrlKeyStateChanged;
                    if (temp != null)
                    {
                        temp(this, new EventArgs());
                    }
                }
            }
        }

        internal event EventHandler<EventArgs> CtrlKeyStateChanged;
    }

    /// <summary>
    /// Listen for the control key being pressed or released to update the CtrlKeyStateChanged for a view.
    /// </summary>
    internal sealed class AsmDocKeyProcessor : KeyProcessor
    {
        CtrlKeyState _state;

        public AsmDocKeyProcessor(CtrlKeyState state)
        {
            _state = state;
        }

        void UpdateState(KeyEventArgs args)
        {
            _state.Enabled = (args.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0 &&
                             (args.KeyboardDevice.Modifiers & ModifierKeys.Shift) == 0;
        }

        public override void PreviewKeyDown(KeyEventArgs args)
        {
            UpdateState(args);
        }

        public override void PreviewKeyUp(KeyEventArgs args)
        {
            UpdateState(args);
        }
    }

    [Export(typeof(IMouseProcessorProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    //[ContentType("code")]
    [Name("AsmDoc")]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)]
    [Order(Before = "WordSelection")]
    internal sealed class AsmDocMouseHandlerProvider : IMouseProcessorProvider
    {
        [Import]
        private IClassifierAggregatorService AggregatorFactory = null;

        [Import]
        private ITextStructureNavigatorSelectorService NavigatorService = null;

        [Import]
        private SVsServiceProvider GlobalServiceProvider = null;

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            //AsmDudeToolsStatic.Output("INFO: AsmDocMouseHandlerProvider:GetAssociatedProcessor: file=" + AsmDudeToolsStatic.GetFileName(view.TextBuffer));

            var buffer = view.TextBuffer;

            IOleCommandTarget shellCommandDispatcher = GetShellCommandDispatcher(view);

            if (shellCommandDispatcher == null)
            {
                return null;
            }

            return new AsmDocMouseHandler(
                view,
                shellCommandDispatcher,
                AggregatorFactory.GetClassifier(buffer),
                NavigatorService.GetTextStructureNavigator(buffer),
                CtrlKeyState.GetStateForView(view),
                AsmDudeTools.Instance);
        }

        #region Private helpers

        /// <summary>
        /// Get the SUIHostCommandDispatcher from the global service provider.
        /// </summary>
        IOleCommandTarget GetShellCommandDispatcher(ITextView view)
        {
            return GlobalServiceProvider.GetService(typeof(SUIHostCommandDispatcher)) as IOleCommandTarget;
        }

        #endregion
    }

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
            //AsmDudeToolsStatic.Output("INFO: AsmDocMouseHandler:constructor: file=" + AsmDudeToolsStatic.GetFileName(view.TextBuffer));
            this._view = view;
            this._commandTarget = commandTarget;
            this._state = state;
            this._aggregator = aggregator;
            this._navigator = navigator;
            this._asmDudeTools = asmDudeTools;

            this._state.CtrlKeyStateChanged += (sender, args) =>
            {
                if (_state.Enabled)
                {
                    this.TryHighlightItemUnderMouse(RelativeToView(Mouse.PrimaryDevice.GetPosition(_view.VisualElement)));
                } else
                {
                    this.SetHighlightSpan(null);
                }
            };

            // Some other points to clear the highlight span:
            _view.LostAggregateFocus += (sender, args) => this.SetHighlightSpan(null);
            _view.VisualElement.MouseLeave += (sender, args) => this.SetHighlightSpan(null);

        }

        #region Mouse processor overrides

        // Remember the location of the mouse on left button down, so we only handle left button up
        // if the mouse has stayed in a single location.
        private Point? _mouseDownAnchorPoint;

        public override void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _mouseDownAnchorPoint = RelativeToView(e.GetPosition(_view.VisualElement));
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            if (!_mouseDownAnchorPoint.HasValue && _state.Enabled && e.LeftButton == MouseButtonState.Released)
            {
                TryHighlightItemUnderMouse(RelativeToView(e.GetPosition(_view.VisualElement)));
            } else if (_mouseDownAnchorPoint.HasValue)
            {
                // Check and see if this is a drag; if so, clear out the highlight.
                var currentMousePosition = RelativeToView(e.GetPosition(_view.VisualElement));
                if (InDragOperation(_mouseDownAnchorPoint.Value, currentMousePosition))
                {
                    _mouseDownAnchorPoint = null;
                    this.SetHighlightSpan(null);
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
            _mouseDownAnchorPoint = null;
        }


        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            try
            {
                if (_mouseDownAnchorPoint.HasValue && this._state.Enabled)
                {
                    var currentMousePosition = RelativeToView(e.GetPosition(_view.VisualElement));

                    if (!InDragOperation(_mouseDownAnchorPoint.Value, currentMousePosition))
                    {
                        this._state.Enabled = false;

                        ITextViewLine line = this._view.TextViewLines.GetTextViewLineContainingYCoordinate(currentMousePosition.Y);
                        SnapshotPoint? bufferPosition = line.GetBufferPositionFromXCoordinate(currentMousePosition.X);
                        string keyword = AsmDudeToolsStatic.getKeywordStr(bufferPosition);
                        if (keyword != null)
                        {
                            this.DispatchGoToDoc(keyword);
                        }
                        this.SetHighlightSpan(null);
                        this._view.Selection.Clear();
                        e.Handled = true;
                    }
                }
                _mouseDownAnchorPoint = null;
            } catch (Exception ex)
            {
                AsmDudeToolsStatic.Output(string.Format("ERROR:{0} PreprocessMouseUp; e={1}", this.ToString(), ex.ToString()));
            }
        }

        #endregion

        #region Private helpers

        private Point RelativeToView(Point position)
        {
            return new Point(position.X + _view.ViewportLeft, position.Y + _view.ViewportTop);
        }

        private bool TryHighlightItemUnderMouse(Point position)
        {
            //AsmDudeToolsStatic.Output("INFO: AsmDocMouseHandler:TryHighlightItemUnderMouse: position=" + position);

            bool updated = false;
            if (!Settings.Default.AsmDoc_On) return false;

            try
            {
                var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y);
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
                var currentSpan = CurrentUnderlineSpan;
                if (currentSpan.HasValue && currentSpan.Value.Contains(bufferPosition.Value))
                {
                    updated = true;
                    return true;
                }

                var extent = _navigator.GetExtentOfWord(bufferPosition.Value);
                if (!extent.IsSignificant)
                {
                    return false;
                }

                //  check for valid classification type.
                foreach (var classification in _aggregator.GetClassificationSpans(extent.Span))
                {
                    string keyword = classification.Span.GetText();
                    //string type = classification.ClassificationType.Classification.ToLower();
                    string url = this.getUrl(keyword);
                    //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:TryHighlightItemUnderMouse: keyword={1}; type={2}; url={3}", this.ToString(), keyword, type, url));
                    if ((url != null) && SetHighlightSpan(classification.Span))
                    {
                        updated = true;
                        return true;
                    }
                }

                // No update occurred, so return false
                return false;
            } finally
            {
                if (!updated)
                {
                    SetHighlightSpan(null);
                }
            }
        }

        private SnapshotSpan? CurrentUnderlineSpan {
            get {
                var classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(_view);
                if (classifier != null && classifier.CurrentUnderlineSpan.HasValue)
                {
                    return classifier.CurrentUnderlineSpan.Value.TranslateTo(_view.TextSnapshot, SpanTrackingMode.EdgeExclusive);
                } else
                {
                    return null;
                }
            }
        }

        private bool SetHighlightSpan(SnapshotSpan? span)
        {
            var classifier = AsmDocUnderlineTaggerProvider.GetClassifierForView(_view);
            if (classifier != null)
            {
                Mouse.OverrideCursor = (span.HasValue) ? Cursors.Hand : null;
                classifier.SetUnderlineSpan(span);
                return true;
            }
            return false;
        }

        private bool DispatchGoToDoc(string keyword)
        {
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:DispatchGoToDoc; keyword=\"{1}\".", this.ToString(), keyword));
            int hr = this.openFile(keyword);
            return ErrorHandler.Succeeded(hr);
        }

        private string getUrl(string keyword)
        {
            string reference = this._asmDudeTools.getUrl(keyword);
            if (reference.Length == 0) return null;
            return Settings.Default.AsmDoc_url + reference;
            //return AsmDudeToolsStatic.getInstallPath() + "html" + Path.DirectorySeparatorChar + reference;
        }

        private int openFile(string keyword)
        {
            string url = this.getUrl(keyword);
            if (url == null)
            { // this situation happens for all keywords (such as registers) that do not have an url specified.
                //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:openFile; url for keyword \"{1}\" is null.", this.ToString(), keyword));
                return 1;
            }
            //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:openFile; url={1}", this.ToString(), url));

            var dte2 = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (dte2 == null)
            {
                AsmDudeToolsStatic.Output(string.Format("WARNING: {0}:openFile; dte2 is null.", this.ToString()));
                return 1;
            } else
            {
                try
                {
                    //dte2.ItemOperations.OpenFile(url, EnvDTE.Constants.vsDocumentKindHTML);
                    dte2.ItemOperations.Navigate(url, EnvDTE.vsNavigateOptions.vsNavigateOptionsNewWindow);
                } catch (Exception e)
                {
                    AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:openFile; exception={1}", this.ToString(), e));
                    return 2;
                }
                return 0;
            }
        }

        #endregion
    }
}
