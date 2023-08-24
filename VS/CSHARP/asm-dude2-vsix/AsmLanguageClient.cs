using AsmDude2.Tools;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AsmDude2
{
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [Export(typeof(ILanguageClient))]
    internal class AsmLanguageClient : ILanguageClient
    {
        public string Name => "Asm Language Extension";

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                yield return "asm";
            }
        }

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        bool ILanguageClient.ShowNotificationOnInitializeFailed => throw new NotImplementedException();

        internal static AsmLanguageClient Instance
        {
            get;
            set;
        }

        public AsmLanguageClient()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "=========================================\nINFO: AsmLanguageClient: Entering constructor"));
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: Entering constructor");
            AsmLanguageClient.Instance = this;
        }

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        event AsyncEventHandler<EventArgs> ILanguageClient.StartAsync
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        event AsyncEventHandler<EventArgs> ILanguageClient.StopAsync
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: ActivateAsync");
            await Task.Yield();

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Server", @"LanguageServerWithUI.exe");
            info.Arguments = AsmDude2Package.AsmDudeContentType;
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;

            Process process = new Process();
            process.StartInfo = info;

            if (process.Start())
            {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public async Task OnLoadedAsync()
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnLoadedAsync");
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            throw new NotImplementedException();
        }
    }
}
