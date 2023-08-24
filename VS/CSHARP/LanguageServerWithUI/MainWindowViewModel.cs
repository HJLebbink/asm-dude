using LanguageServer;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LanguageServerWithUI
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private string logMessage;
        private ObservableCollection<DiagnosticItem> diagnosticItems = new ObservableCollection<DiagnosticItem>();
        private ObservableCollection<FoldingRangeItem> foldingRanges = new ObservableCollection<FoldingRangeItem>();
        private ObservableCollection<SymbolInformationItem> symbols = new ObservableCollection<SymbolInformationItem>();
        private readonly LanguageServer.LanguageServer languageServer;
        private string initializedMessage;
        private string responseText;
        private string currentSettings;
        private MessageType messageType;
        private string messageRequestOptions;
        private string lastCompletionRequest;
        private bool useItemCommitCharacters;
        private bool useNonInsertingCommitCharacters;
        private bool useServerCommitCharacters = true;

        public MainWindowViewModel()
        {
            var stdInPipeName = @"input";
            var stdOutPipeName = @"output";

            var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var readerPipe = new NamedPipeClientStream(stdInPipeName);
            var writerPipe = new NamedPipeClientStream(stdOutPipeName);

            readerPipe.Connect();
            writerPipe.Connect();

            this.InitializedMessage = "The server has not yet been initialized.";
            this.languageServer = new LanguageServer.LanguageServer(writerPipe, readerPipe);

            this.languageServer.OnInitialized += OnInitialized;
            this.languageServer.Disconnected += OnDisconnected;
            this.languageServer.PropertyChanged += OnLanguageServerPropertyChanged;

            DiagnosticItems.Add(new DiagnosticItem());
            this.LogMessage = string.Empty;
            this.ResponseText = string.Empty;
            this.MessageRequestOptions = "3";
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            this.InitializedMessage = "The server has been initialized!";
        }

        private void OnLanguageServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals(nameof(LanguageServer.LanguageServer.CurrentSettings)))
            {
                this.CurrentSettings = this.languageServer.CurrentSettings;
            }
            else if (e.PropertyName.Equals(nameof(LanguageServer.LanguageServer.LastCompletionRequest)))
            {
                this.LastCompletionRequest = this.languageServer.LastCompletionRequest;
            }
            else if (e.PropertyName.Equals(nameof(LanguageServer.LanguageServer.IsIncomplete)))
            {
                this.IsIncomplete = this.languageServer.IsIncomplete;
            }
        }

        internal void SendLogMessage()
        {
            this.languageServer.LogMessage(arg: null, message: this.LogMessage, messageType: this.MessageType);
        }

        internal void SendMessage()
        {
            this.languageServer.ShowMessage(message: this.LogMessage, messageType: this.MessageType);
        }

        internal void SendMessageRequest()
        {
            var response = Task.Run(async () =>
            {
                List<string> options = new List<string>();
                int optionsCount = 0;
                optionsCount = int.TryParse(MessageRequestOptions, out optionsCount) ? Math.Min(optionsCount, 1000) : 3;

                for (int i = 0; i < optionsCount; i++)
                {
                    options.Add($"option {i}");
                }

                MessageActionItem selectedAction = await this.languageServer.ShowMessageRequestAsync(message: this.LogMessage, messageType: this.MessageType, actionItems: options.ToArray());
                this.ResponseText = $"The user selected: {selectedAction?.Title ?? "cancelled"}";
            });
        }

        private void OnDisconnected(object sender, System.EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<DiagnosticItem> DiagnosticItems
        {
            get { return this.diagnosticItems; }
        }

        public ObservableCollection<SymbolInformationItem> Symbols
        {
            get { return this.symbols; }
        }

        public ObservableCollection<FoldingRangeItem> FoldingRanges
        {
            get { return this.foldingRanges; }
        }

        public string InitializedMessage
        {
            get
            {
                return this.initializedMessage;
            }
            set
            {
                this.initializedMessage = value;
                this.NotifyPropertyChanged(nameof(InitializedMessage));
            }
        }

        public string LogMessage
        {
            get
            {
                return this.logMessage;
            }
            set
            {
                this.logMessage = value;
                this.NotifyPropertyChanged(nameof(LogMessage));
            }
        }

        public string ResponseText
        {
            get
            {
                return this.responseText;
            }
            set
            {
                this.responseText = value;
                this.NotifyPropertyChanged(nameof(ResponseText));
            }
        }

        public string CustomText
        {
            get
            {
                return this.languageServer.CustomText;
            }
            set
            {
                this.languageServer.CustomText = value;
                this.NotifyPropertyChanged(nameof(CustomText));
            }
        }

        public string ReferenceToFind
        {
            get;
            set;
        }

        public int ReferencesChunkSize
        {
            get;
            set;
        }

        public int ReferencesDelay
        {
            get;
            set;
        }

        public int HighlightsChunkSize
        {
            get;
            set;
        }

        public int HighlightsDelay
        {
            get;
            set;
        }

        public string ApplyTextEditText
        {
            get;
            set;
        }

        public MessageType MessageType
        {
            get
            {
                return this.messageType;
            }
            set
            {
                this.messageType = value;
                this.NotifyPropertyChanged(nameof(MessageType));
            }
        }

        public bool IsIncomplete
        {
            get
            {
                return this.languageServer.IsIncomplete;
            }
            set
            {
                this.languageServer.IsIncomplete = value;
                this.NotifyPropertyChanged(nameof(IsIncomplete));
            }
        }

        public bool CompletionServerError
        {
            get
            {
                return this.languageServer.CompletionServerError;
            }
            set
            {
                this.languageServer.CompletionServerError = value;
                this.NotifyPropertyChanged(nameof(CompletionServerError));
            }
        }

        public bool UseServerCommitCharacters
        {
            get
            {
                return this.useServerCommitCharacters;
            }
            set
            {
                this.useServerCommitCharacters = value;
                if (value)
                {
                    this.languageServer.ItemCommitCharacters = false;
                }
                this.NotifyPropertyChanged(nameof(UseServerCommitCharacters));
            }
        }

        public bool UseItemCommitCharacters
        {
            get
            {
                return this.useItemCommitCharacters;
            }
            set
            {
                this.useItemCommitCharacters = value;
                if (value)
                {
                    this.languageServer.ItemCommitCharacters = true;
                }
                this.NotifyPropertyChanged(nameof(UseItemCommitCharacters));
            }
        }

        public string LastCompletionRequest
        {
            get
            {
                return this.lastCompletionRequest;
            }
            set
            {
                this.lastCompletionRequest = value;
                this.NotifyPropertyChanged(nameof(LastCompletionRequest));
            }
        }

        public string CurrentSettings
        {
            get
            {
                return this.currentSettings;
            }
            set
            {
                this.currentSettings = value;
                this.NotifyPropertyChanged(nameof(CurrentSettings));
            }
        }

        public string MessageRequestOptions
        {
            get
            {
                return this.messageRequestOptions;
            }
            set
            {
                this.messageRequestOptions = value;
                this.NotifyPropertyChanged(nameof(MessageRequestOptions));
            }
        }

        public bool UsePublishModelDiagnostic
        {
            get
            {
                return this.languageServer.UsePublishModelDiagnostic;
            }
            set
            {
                this.languageServer.UsePublishModelDiagnostic = value;
                this.NotifyPropertyChanged(nameof(UsePublishModelDiagnostic));
            }
        }

        public void SendDiagnostics(bool pushDiagnostics = true)
        {
            var diagnostics = new List<DiagnosticsInfo>();

            for (int i = 0; i < DiagnosticItems.Count; i++)
            {
                var diagnostic = DiagnosticItems[i];
                if ((int)diagnostic.Severity != 0 && (int)diagnostic.Tag != 0)
                {
                    diagnostics.Add(new DiagnosticsInfo(diagnostic.Text, diagnostic.Context.ToVSContext(), (DiagnosticTag)diagnostic.Tag, diagnostic.Severity));
                }
            }

            this.languageServer.SendDiagnostics(diagnostics, pushDiagnostics);
        }

        public void SetReferences()
        {
            this.languageServer.SetFindReferencesParams(this.ReferenceToFind, this.ReferencesChunkSize, this.ReferencesDelay);
        }

        public void SetDocumentHighlights()
        {
            this.languageServer.SetDocumentHighlightsParams(this.HighlightsChunkSize, this.HighlightsDelay);
        }

        public void SetFoldingRanges()
        {
            var foldingRanges = this.FoldingRanges.Select(f => new FoldingRange() { StartLine = f.StartLine, StartCharacter = f.StartCharacter, EndLine = f.EndLine, EndCharacter = f.EndCharacter, Kind = FoldingRangeKind.Comment });
            this.languageServer.SetFoldingRanges(foldingRanges);
        }

        public void SetSymbols()
        {
            List<VSSymbolInformation> symbols = new List<VSSymbolInformation>();
            foreach (SymbolInformationItem item in this.Symbols)
            {
                var symbol = new VSSymbolInformation()
                {
                    Name = item.Name,
                    Kind = item.Kind,
                    ContainerName = item.Container,
                };

                symbols.Add(symbol);
            }

            this.languageServer.SetDocumentSymbols(symbols);
        }

        public void ApplyTextEdit()
        {
            this.languageServer.ApplyTextEdit(this.ApplyTextEditText);
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
