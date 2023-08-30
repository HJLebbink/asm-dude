using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel.Composition;
using AsmDude2.Tools;

namespace AsmDude2
{
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [Export(typeof(ILanguageClient))]
    [RunOnContext(RunningContext.RunOnHost)]
    public class AsmLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        public AsmLanguageClient()
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: Entering constructor");
            Instance = this;
        }

        internal static AsmLanguageClient Instance
        {
            get;
            set;
        }

        internal JsonRpc Rpc
        {
            get;
            set;
        }

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public string Name => "Asm Language Extension";

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: get ConfigurationSections");
                yield return "foo";
            }
        }

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public object MiddleLayer
        {
            get;
            set;
        }

        public object CustomMessageTarget => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            // Debugger.Launch();

            string programPath = Path.Combine(AsmDudeToolsStatic.Get_Install_Path(), "Server", "LanguageServerWithUI.exe");
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: ActivateAsync: configuring language server " + programPath);

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = programPath,
                WorkingDirectory = Path.GetDirectoryName(programPath)
            };

            var stdInPipeName = @"output";
            var stdOutPipeName = @"input";

            var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var bufferSize = 256;
            var readerPipe = new NamedPipeServerStream(stdInPipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize, pipeSecurity);
            var writerPipe = new NamedPipeServerStream(stdOutPipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize, pipeSecurity);

            Process process = new Process
            {
                StartInfo = info
            };

            if (process.Start())
            {
                await readerPipe.WaitForConnectionAsync(token);
                await writerPipe.WaitForConnectionAsync(token);
                return new Connection(readerPipe, writerPipe);
            }

            return null;
        }

        public async Task OnLoadedAsync()
        {
            if (StartAsync != null)
            {
                AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnLoadedAsync");
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public async Task StopServerAsync()
        {
            if (StopAsync != null)
            {
                AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: StopServerAsync");
                await StopAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public Task OnServerInitializedAsync()
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnServerInitializedAsync");
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: AttachForCustomMessageAsync");
            this.Rpc = rpc;
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnServerInitializeFailedAsync");
            string message = "Asm Language Client failed to activate";
            string exception = initializationState.InitializationException?.ToString() ?? string.Empty;

            var failureContext = new InitializationFailureContext()
            {
                FailureMessage = $"{message}\n {exception}",
            };

            return Task.FromResult(failureContext);
        }

        internal class FooMiddleLayer : ILanguageClientMiddleLayer
        {
            public bool CanHandle(string methodName)
            {
                return methodName == Methods.TextDocumentCompletionName;
            }

            public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
            {
                throw new NotImplementedException();
            }

            public async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
            {
                var result = await sendRequest(methodParam);
                return result;
            }
        }
    }
}
