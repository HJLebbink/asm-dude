/*

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading;

using Microsoft.VisualStudio.Imaging.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;


namespace AsmDude.LightBulb {

    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Test Suggested Actions")]
    [ContentType("asm!")]
    internal class TestSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider {

        [Import(typeof(ITextStructureNavigatorSelectorService))]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer) {
            if (textBuffer == null && textView == null) {
                return null;
            }
            return new TestSuggestedActionsSource(this, textView, textBuffer);
        }
    }

    internal class TestSuggestedActionsSource : ISuggestedActionsSource {
        private readonly TestSuggestedActionsSourceProvider m_factory;
        private readonly ITextBuffer m_textBuffer;
        private readonly ITextView m_textView;

        public TestSuggestedActionsSource(TestSuggestedActionsSourceProvider testSuggestedActionsSourceProvider, ITextView textView, ITextBuffer textBuffer) {
            m_factory = testSuggestedActionsSourceProvider;
            m_textBuffer = textBuffer;
            m_textView = textView;
        }

        private bool TryGetWordUnderCaret(out TextExtent wordExtent) {
            ITextCaret caret = m_textView.Caret;
            SnapshotPoint point;

            if (caret.Position.BufferPosition > 0) {
                point = caret.Position.BufferPosition - 1;
            } else {
                wordExtent = default(TextExtent);
                return false;
            }

            ITextStructureNavigator navigator = m_factory.NavigatorService.GetTextStructureNavigator(m_textBuffer);

            wordExtent = navigator.GetExtentOfWord(point);
            return true;
        }

        public Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            return Task.Factory.StartNew(() => {
                TextExtent extent;
                if (TryGetWordUnderCaret(out extent)) {
                    // don't display the action if the extent has whitespace
                    return extent.IsSignificant;
                }
                return false;
            });
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken) {
            TextExtent extent;
            if (TryGetWordUnderCaret(out extent) && extent.IsSignificant) {
                ITrackingSpan trackingSpan = range.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
                var upperAction = new UpperCaseSuggestedAction(trackingSpan);
                var lowerAction = new LowerCaseSuggestedAction(trackingSpan);
                return new SuggestedActionSet[] { new SuggestedActionSet(new ISuggestedAction[] { upperAction, lowerAction }) };
            }
            return Enumerable.Empty<SuggestedActionSet>();
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged;


        public void Dispose() {
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            // This is a sample provider and doesn't participate in LightBulb telemetry
            telemetryId = Guid.Empty;
            return false;
        }
    }

    internal class UpperCaseSuggestedAction : ISuggestedAction {
        private ITrackingSpan m_span;
        private string m_upper;
        private string m_display;
        private ITextSnapshot m_snapshot;

        public UpperCaseSuggestedAction(ITrackingSpan span) {
            m_span = span;
            m_snapshot = span.TextBuffer.CurrentSnapshot;
            m_upper = span.GetText(m_snapshot).ToUpper();
            m_display = string.Format("Convert '{0}' to upper case", span.GetText(m_snapshot));
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
            var textBlock = new TextBlock();
            textBlock.Padding = new Thickness(5);
            textBlock.Inlines.Add(new Run() { Text = m_upper });
            return Task.FromResult<object>(textBlock);
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        public bool HasActionSets {
            get { return false; }
        }
        public string DisplayText {
            get { return m_display; }
        }
        public ImageMoniker IconMoniker {
            get { return default(ImageMoniker); }
        }
        public string IconAutomationText {
            get {
                return null;
            }
        }
        public string InputGestureText {
            get {
                return null;
            }
        }
        public bool HasPreview {
            get { return true; }
        }

        public void Invoke(CancellationToken cancellationToken) {
            m_span.TextBuffer.Replace(m_span.GetSpan(m_snapshot), m_upper);
        }
        public void Dispose() {
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            // This is a sample action and doesn't participate in LightBulb telemetry
            telemetryId = Guid.Empty;
            return false;
        }
    }

    internal class LowerCaseSuggestedAction : ISuggestedAction {
        private ITrackingSpan m_span;
        private string m_upper;
        private string m_display;
        private ITextSnapshot m_snapshot;

        public LowerCaseSuggestedAction(ITrackingSpan span) {
            m_span = span;
            m_snapshot = span.TextBuffer.CurrentSnapshot;
            m_upper = span.GetText(m_snapshot).ToLower();
            m_display = string.Format("Convert '{0}' to lower case", span.GetText(m_snapshot));
        }

        public Task<object> GetPreviewAsync(CancellationToken cancellationToken) {
            var textBlock = new TextBlock();
            textBlock.Padding = new Thickness(5);
            textBlock.Inlines.Add(new Run() { Text = m_upper });
            return Task.FromResult<object>(textBlock);
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken) {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }

        public bool HasActionSets {
            get { return false; }
        }
        public string DisplayText {
            get { return m_display; }
        }
        public ImageMoniker IconMoniker {
            get { return default(ImageMoniker); }
        }
        public string IconAutomationText {
            get {
                return null;
            }
        }
        public string InputGestureText {
            get {
                return null;
            }
        }
        public bool HasPreview {
            get { return true; }
        }

        public void Invoke(CancellationToken cancellationToken) {
            m_span.TextBuffer.Replace(m_span.GetSpan(m_snapshot), m_upper);
        }
        public void Dispose() {
        }

        public bool TryGetTelemetryId(out Guid telemetryId) {
            // This is a sample action and doesn't participate in LightBulb telemetry
            telemetryId = Guid.Empty;
            return false;
        }
    }
}
*/