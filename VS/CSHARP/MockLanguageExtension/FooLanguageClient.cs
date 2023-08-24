using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel.Composition;

namespace MockLanguageExtension
{
    [ContentType("foo")]
    [Export(typeof(ILanguageClient))]
    [RunOnContext(RunningContext.RunOnHost)]
    public class FooLanguageClient : ILanguageClient, ILanguageClientCustomMessage2
    {
        public FooLanguageClient()
        {
            Instance = this;
        }

        internal static FooLanguageClient Instance
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

        public string Name => "Foo Language Extension";

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
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

            ProcessStartInfo info = new ProcessStartInfo();
            var programPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Server", @"LanguageServerWithUI.exe");
            info.FileName = programPath;
            info.WorkingDirectory = Path.GetDirectoryName(programPath);

            var stdInPipeName = @"output";
            var stdOutPipeName = @"input";

            var pipeAccessRule = new PipeAccessRule("Everyone", PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(pipeAccessRule);

            var bufferSize = 256;
            var readerPipe = new NamedPipeServerStream(stdInPipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize, pipeSecurity);
            var writerPipe = new NamedPipeServerStream(stdOutPipeName, PipeDirection.InOut, 4, PipeTransmissionMode.Message, PipeOptions.Asynchronous, bufferSize, bufferSize, pipeSecurity);

            Process process = new Process();
            process.StartInfo = info;

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
                await StartAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public async Task StopServerAsync()
        {
            if (StopAsync != null)
            {
                await StopAsync.InvokeAsync(this, EventArgs.Empty);
            }
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            this.Rpc = rpc;

            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            string message = "Oh no! Foo Language Client failed to activate, now we can't test LSP! :(";
            string exception = initializationState.InitializationException?.ToString() ?? string.Empty;
            message = $"{message}\n {exception}";

            var failureContext = new InitializationFailureContext()
            {
                FailureMessage = message,
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
