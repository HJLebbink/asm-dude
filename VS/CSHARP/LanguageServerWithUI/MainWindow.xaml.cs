using Microsoft.VisualStudio.LanguageServer.Protocol;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace LanguageServerWithUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const Key ShowMessageRequestGlobalKey = Key.F9;

        private const ModifierKeys ShowMessageRequestGlobalModifierKeys = ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Control;

        private static int ShowMessageRequestGlobalVirtualKeyCode
        {
            get
            {
                return KeyInterop.VirtualKeyFromKey(ShowMessageRequestGlobalKey);
            }
        }

        private static int ShowMessageRequestGlobalId
        {
            get
            {
                return ShowMessageRequestGlobalVirtualKeyCode + ((int)ShowMessageRequestGlobalModifierKeys * 0x10000);
            }
        }

        private MainWindowViewModel viewModel = new MainWindowViewModel();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OnAddDiagnosticButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.DiagnosticItems.Add(new DiagnosticItem());
        }

        private void OnClearDiagnosticButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.DiagnosticItems.Clear();
        }

        private void OnSendDiagnosticsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SendDiagnostics();
        }

        private void OnSetReferencesButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SetReferences();
        }

        private void OnSetHighlightsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SetDocumentHighlights();
        }

        private void OnAddFoldingRangeItemButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.FoldingRanges.Add(new FoldingRangeItem());
        }

        private void OnClearFoldingRangeItemsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.FoldingRanges.Clear();
        }

        private void OnSetFoldingRangeItemsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SetFoldingRanges();
        }

        private void OnApplyTextEditButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.ApplyTextEdit();
        }

        private void OnShowMessageButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SendMessage();
        }

        private void OnShowMessageRequestButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SendMessageRequest();
        }

        private void OnLogMessageButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SendLogMessage();
        }

        private void OnAddSymbolButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.Symbols.Add(new SymbolInformationItem());
        }

        private void OnClearSymbolsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.Symbols.Clear();
        }

        private void OnSetSymbolsButtonClick(object sender, RoutedEventArgs e)
        {
            viewModel.SetSymbols();
        }

        public override void BeginInit()
        {
            base.BeginInit();

            // Add a global hotkey for CTRL + SHIFT + ALT + F9 to show the message request to support
            // accessibility testing.
            RegisterHotKey(IntPtr.Zero, ShowMessageRequestGlobalId, (uint)ShowMessageRequestGlobalModifierKeys, (uint)ShowMessageRequestGlobalVirtualKeyCode);
            ComponentDispatcher.ThreadFilterMessage += this.ComponentDispatcher_ThreadFilterMessage;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            UnregisterHotKey(IntPtr.Zero, ShowMessageRequestGlobalId);
            ComponentDispatcher.ThreadFilterMessage -= this.ComponentDispatcher_ThreadFilterMessage;

            base.OnClosing(e);
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled)
            {
                // If the message is WM_HOTKEY
                if (msg.message == 0x0312)
                {
                    int id = (int)msg.wParam;
                    if (id == ShowMessageRequestGlobalId)
                    {
                        this.OnShowMessageRequestButtonClick(this, new RoutedEventArgs());
                        handled = true;
                    }
                }
            }
        }
    }
}
