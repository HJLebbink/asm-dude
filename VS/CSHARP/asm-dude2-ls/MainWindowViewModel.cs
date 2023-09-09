using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;

namespace AsmDude2LS
{
    public class MainWindowViewModel //: INotifyPropertyChanged
    {
        private readonly LanguageServer languageServer;

        public MainWindowViewModel()
        {
            const string stdInPipeName = @"input";
            const string stdOutPipeName = @"output";

            var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var readerPipe = new NamedPipeClientStream(stdInPipeName);
            var writerPipe = new NamedPipeClientStream(stdOutPipeName);

            readerPipe.Connect();
            writerPipe.Connect();

            this.languageServer = new LanguageServer(writerPipe, readerPipe);
            this.languageServer.Disconnected += OnDisconnected;
        }

        private void OnDisconnected(object sender, System.EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }
    }
}
