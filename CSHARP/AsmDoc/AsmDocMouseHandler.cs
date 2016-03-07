using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Shell;
using System.Diagnostics;
using System.Globalization;
using AsmDude;
using System.Reflection;
using EnvDTE80;
using System.IO;

namespace AsmDude.GoToDoc {

    [Export(typeof(IKeyProcessorProvider))]
    // [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType("code")]
    [Name("AsmDoc")]
    [Order(Before = "VisualStudioKeyboardProcessor")]
    internal sealed class AsmDocKeyProcessorProvider : IKeyProcessorProvider {
        public KeyProcessor GetAssociatedProcessor(IWpfTextView view) {
            return view.Properties.GetOrCreateSingletonProperty(typeof(AsmDocKeyProcessor), () => new AsmDocKeyProcessor(CtrlKeyState.GetStateForView(view)));
        }
    }

    /// <summary>
    /// The state of the control key for a given view, which is kept up-to-date by a combination of the
    /// key processor and the mouse process
    /// </summary>
    internal sealed class CtrlKeyState {
        internal static CtrlKeyState GetStateForView(ITextView view) {
            return view.Properties.GetOrCreateSingletonProperty(typeof(CtrlKeyState), () => new CtrlKeyState());
        }

        bool _enabled = false;

        internal bool Enabled {
            get {
                // Check and see if ctrl is down but we missed it somehow.
                bool ctrlDown = (Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                                (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
                if (ctrlDown != _enabled)
                    Enabled = ctrlDown;

                return _enabled;
            }
            set {
                bool oldVal = _enabled;
                _enabled = value;
                if (oldVal != _enabled) {
                    var temp = CtrlKeyStateChanged;
                    if (temp != null)
                        temp(this, new EventArgs());
                }
            }
        }

        internal event EventHandler<EventArgs> CtrlKeyStateChanged;
    }

    /// <summary>
    /// Listen for the control key being pressed or released to update the CtrlKeyStateChanged for a view.
    /// </summary>
    internal sealed class AsmDocKeyProcessor : KeyProcessor {
        CtrlKeyState _state;

        public AsmDocKeyProcessor(CtrlKeyState state) {
            _state = state;
        }

        void UpdateState(KeyEventArgs args) {
            _state.Enabled = (args.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0 &&
                             (args.KeyboardDevice.Modifiers & ModifierKeys.Shift) == 0;
        }

        public override void PreviewKeyDown(KeyEventArgs args) {
            UpdateState(args);
        }

        public override void PreviewKeyUp(KeyEventArgs args) {
            UpdateState(args);
        }
    }

    [Export(typeof(IMouseProcessorProvider))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType("code")]
    [Name("AsmDoc")]
    [Order(Before = "WordSelection")]
    internal sealed class AsmDocMouseHandlerProvider : IMouseProcessorProvider {
        [Import]
        IClassifierAggregatorService AggregatorFactory = null;

        [Import]
        ITextStructureNavigatorSelectorService NavigatorService = null;

        [Import]
        SVsServiceProvider GlobalServiceProvider = null;

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView view) {
            var buffer = view.TextBuffer;

            IOleCommandTarget shellCommandDispatcher = GetShellCommandDispatcher(view);

            if (shellCommandDispatcher == null) {
                return null;
            }
            return new AsmDocMouseHandler(view,
                                           shellCommandDispatcher,
                                           AggregatorFactory.GetClassifier(buffer),
                                           NavigatorService.GetTextStructureNavigator(buffer),
                                           CtrlKeyState.GetStateForView(view));
        }

        #region Private helpers

        /// <summary>
        /// Get the SUIHostCommandDispatcher from the global service provider.
        /// </summary>
        IOleCommandTarget GetShellCommandDispatcher(ITextView view) {
            return GlobalServiceProvider.GetService(typeof(SUIHostCommandDispatcher)) as IOleCommandTarget;
        }

        #endregion
    }

    /// <summary>
    /// Handle ctrl+click on valid elements to send GoToDefinition to the shell.  Also handle mouse moves
    /// (when control is pressed) to highlight references for which GoToDefinition will (likely) be valid.
    /// </summary>
    internal sealed class AsmDocMouseHandler : MouseProcessorBase {
        IWpfTextView _view;
        CtrlKeyState _state;
        IClassifier _aggregator;
        ITextStructureNavigator _navigator;
        IOleCommandTarget _commandTarget;

        [Import]
        private AsmDudeTools _asmDudeTools = null;


        public AsmDocMouseHandler(IWpfTextView view, IOleCommandTarget commandTarget, IClassifier aggregator,
                                   ITextStructureNavigator navigator, CtrlKeyState state) {
            _view = view;
            _commandTarget = commandTarget;
            _state = state;
            _aggregator = aggregator;
            _navigator = navigator;

            _state.CtrlKeyStateChanged += (sender, args) => {
                if (_state.Enabled) {
                    this.TryHighlightItemUnderMouse(RelativeToView(Mouse.PrimaryDevice.GetPosition(_view.VisualElement)));
                } else {
                    this.SetHighlightSpan(null);
                }
            };

            // resolve _asmDudeTools
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

            // Some other points to clear the highlight span:
            _view.LostAggregateFocus += (sender, args) => this.SetHighlightSpan(null);
            _view.VisualElement.MouseLeave += (sender, args) => this.SetHighlightSpan(null);

         }

        #region Mouse processor overrides

        // Remember the location of the mouse on left button down, so we only handle left button up
        // if the mouse has stayed in a single location.
        Point? _mouseDownAnchorPoint;

        public override void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e) {
            _mouseDownAnchorPoint = RelativeToView(e.GetPosition(_view.VisualElement));
        }

        public override void PreprocessMouseMove(MouseEventArgs e) {
            if (!_mouseDownAnchorPoint.HasValue && _state.Enabled && e.LeftButton == MouseButtonState.Released) {
                TryHighlightItemUnderMouse(RelativeToView(e.GetPosition(_view.VisualElement)));
            } else if (_mouseDownAnchorPoint.HasValue) {
                // Check and see if this is a drag; if so, clear out the highlight.
                var currentMousePosition = RelativeToView(e.GetPosition(_view.VisualElement));
                if (InDragOperation(_mouseDownAnchorPoint.Value, currentMousePosition)) {
                    _mouseDownAnchorPoint = null;
                    this.SetHighlightSpan(null);
                }
            }
        }

        private bool InDragOperation(Point anchorPoint, Point currentPoint) {
            // If the mouse up is more than a drag away from the mouse down, this is a drag
            return Math.Abs(anchorPoint.X - currentPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                   Math.Abs(anchorPoint.Y - currentPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
        }

        public override void PreprocessMouseLeave(MouseEventArgs e) {
            _mouseDownAnchorPoint = null;
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e) {
            if (_mouseDownAnchorPoint.HasValue && _state.Enabled) {
                var currentMousePosition = RelativeToView(e.GetPosition(_view.VisualElement));

                if (!InDragOperation(_mouseDownAnchorPoint.Value, currentMousePosition)) {
                    _state.Enabled = false;

                    var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(currentMousePosition.Y);
                    var bufferPosition = line.GetBufferPositionFromXCoordinate(currentMousePosition.X);
                    if (bufferPosition != null) {
                        string lineText = bufferPosition.Value.GetContainingLine().GetText().Trim();
                        //TODO: get the right word at the buffer position instead of the first keyword on the line
                        string[] words = lineText.Split(' ','\t',',','[',']');
                        string keyword = words[0].Trim();
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:PreprocessMouseUp; lineText={1}; keyword={2}", this.ToString(), lineText, keyword));
                        this.DispatchGoToDoc(keyword);
                    }
                    this.SetHighlightSpan(null);
                    _view.Selection.Clear();
                    e.Handled = true;
                }
            }

            _mouseDownAnchorPoint = null;
        }

        #endregion

        #region Private helpers

        Point RelativeToView(Point position) {
            return new Point(position.X + _view.ViewportLeft, position.Y + _view.ViewportTop);
        }

        bool TryHighlightItemUnderMouse(Point position) {


            bool updated = false;
            if (!Properties.Settings.Default.AsmDoc_On) return false;

            try {
                var line = _view.TextViewLines.GetTextViewLineContainingYCoordinate(position.Y);
                if (line == null) {
                    return false;
                }
                var bufferPosition = line.GetBufferPositionFromXCoordinate(position.X);
                if (!bufferPosition.HasValue) {
                    return false;
                }

                // Quick check - if the mouse is still inside the current underline span, we're already set
                var currentSpan = CurrentUnderlineSpan;
                if (currentSpan.HasValue && currentSpan.Value.Contains(bufferPosition.Value)) {
                    updated = true;
                    return true;
                }

                var extent = _navigator.GetExtentOfWord(bufferPosition.Value);
                if (!extent.IsSignificant) {
                    return false;
                }

                //  check for valid classification type.
                foreach (var classification in _aggregator.GetClassificationSpans(extent.Span)) {
                    string keyword = classification.Span.GetText();
                    string type = classification.ClassificationType.Classification.ToLower();
                    string url = this.getUrl(keyword);
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:TryHighlightItemUnderMouse: keyword={1}; type={2}; url={3}", this.ToString(), keyword, type, url));


                    //if ((type.Equals("mnemonic") || type.Equals("jump")) && SetHighlightSpan(classification.Span)) {
                    if ((url != null) && SetHighlightSpan(classification.Span)) {
                        updated = true;
                        return true;
                    }
                }

                // No update occurred, so return false
                return false;
            } finally {
                if (!updated) {
                    SetHighlightSpan(null);
                }
            }
        }

        SnapshotSpan? CurrentUnderlineSpan {
            get {
                var classifier = UnderlineClassifierProvider.GetClassifierForView(_view);
                if (classifier != null && classifier.CurrentUnderlineSpan.HasValue) {
                    return classifier.CurrentUnderlineSpan.Value.TranslateTo(_view.TextSnapshot, SpanTrackingMode.EdgeExclusive);
                } else {
                    return null;
                }
            }
        }

        private bool SetHighlightSpan(SnapshotSpan? span) {
            var classifier = UnderlineClassifierProvider.GetClassifierForView(_view);
            if (classifier != null) {
                Mouse.OverrideCursor = (span.HasValue) ? Cursors.Hand : null;
                classifier.SetUnderlineSpan(span);
                return true;
            }
            return false;
        }

        private bool DispatchGoToDoc(string keyword) {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:DispatchGoToDoc; keyword={1}", this.ToString(), keyword));

            int hr = this.openFile(keyword);
            return ErrorHandler.Succeeded(hr);
            /*
                Guid cmdGroup = VSConstants.GUID_VSStandardCommandSet97;
                int hr = _commandTarget.Exec(ref cmdGroup,
                            (uint)VSConstants.VSStd97CmdID.GotoDefn,
                            (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT,
                            System.IntPtr.Zero,
                            System.IntPtr.Zero);
            */
        }

        private string getUrl(string keyword) {
            string reference = this._asmDudeTools.getUrl(keyword);
            if (reference == null) return null;
            if (reference.Length == 0) return null;

            return Properties.Settings.Default.AsmDoc_url + reference;
            //return "http://www.felixcloutier.com/x86/" + reference;
            //return AsmDudeToolsStatic.getInstallPath() + "html" + Path.DirectorySeparatorChar + reference;
        }

        private int openFile(string keyword) {

            string url = this.getUrl(keyword);
            if (url == null) return 1;

            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:openFile; url={1}", this.ToString(), url));

            var dte2 = Package.GetGlobalService(typeof(SDTE)) as DTE2;
            if (dte2 == null) {
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "WARNING: {0}:DispatchGoToDoc; dte2 is null", this.ToString()));
                return 1;
            } else {
                try {
                    //dte2.ItemOperations.OpenFile(url, EnvDTE.Constants.vsDocumentKindHTML);
                    dte2.ItemOperations.Navigate(url, EnvDTE.vsNavigateOptions.vsNavigateOptionsNewWindow);
                } catch (Exception e) {
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "ERROR: {0}:DispatchGoToDoc; exception={1}", this.ToString(), e));
                    return 2;
                }
                return 0;
            }
        }

        #endregion
    }
}
