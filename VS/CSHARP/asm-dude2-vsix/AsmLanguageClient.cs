// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using System.ComponentModel.Composition;
using AsmDude2.Tools;
using AsmTools;
using System.Windows.Forms;
using System.Security.Principal;

namespace AsmDude2
{
    //[ContentType(AsmDude2Package.DisassemblyContentType)]
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [Export(typeof(ILanguageClient))]
    //[RunOnContext(RunningContext.RunOnHost)]
    public class AsmLanguageClient : ILanguageClient
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

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public string Name => "AsmDude2";

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: get ConfigurationSections");
                yield return "asm";
            }
        }

        public object InitializationOptions
        {
            get
            {
                AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: get InitializationOptions");
                return new AsmLanguageServerOptions
                {
                    SyntaxHighlighting_Opcode = Settings.Default.SyntaxHighlighting_Opcode,
                    SyntaxHighlighting_Register = Settings.Default.SyntaxHighlighting_Register,
                    SyntaxHighlighting_Remark = Settings.Default.SyntaxHighlighting_Remark,
                    SyntaxHighlighting_Directive = Settings.Default.SyntaxHighlighting_Directive,
                    SyntaxHighlighting_Jump = Settings.Default.SyntaxHighlighting_Jump,
                    SyntaxHighlighting_Label = Settings.Default.SyntaxHighlighting_Label,
                    SyntaxHighlighting_Constant = Settings.Default.SyntaxHighlighting_Constant,
                    SyntaxHighlighting_Misc = Settings.Default.SyntaxHighlighting_Misc,
                    SyntaxHighlighting_On = Settings.Default.SyntaxHighlighting_On,
                    CodeFolding_On = Settings.Default.CodeFolding_On,
                    CodeFolding_BeginTag = Settings.Default.CodeFolding_BeginTag,
                    CodeFolding_EndTag = Settings.Default.CodeFolding_EndTag,
                    CodeCompletion_On = Settings.Default.CodeCompletion_On,
                    AsmDoc_Url = Settings.Default.AsmDoc_Url,
                    AsmDoc_On = Settings.Default.AsmDoc_On,
                    useAssemblerMasm = Settings.Default.useAssemblerMasm,
                    useAssemblerNasm = Settings.Default.useAssemblerNasm,
                    IntelliSense_Show_Undefined_Labels = Settings.Default.IntelliSense_Show_Undefined_Labels,
                    IntelliSense_Show_Clashing_Labels = Settings.Default.IntelliSense_Show_Clashing_Labels,
                    IntelliSense_Decorate_Undefined_Labels = Settings.Default.IntelliSense_Decorate_Undefined_Labels,
                    IntelliSense_Decorate_Clashing_Labels = Settings.Default.IntelliSense_Decorate_Clashing_Labels,
                    ARCH_8086 = Settings.Default.ARCH_8086,
                    ARCH_186 = Settings.Default.ARCH_186,
                    ARCH_286 = Settings.Default.ARCH_286,
                    ARCH_386 = Settings.Default.ARCH_386,
                    ARCH_486 = Settings.Default.ARCH_486,
                    ARCH_MMX = Settings.Default.ARCH_MMX,
                    ARCH_SSE = Settings.Default.ARCH_SSE,
                    ARCH_SSE2 = Settings.Default.ARCH_SSE2,
                    ARCH_SSE3 = Settings.Default.ARCH_SSE3,
                    ARCH_SSSE3 = Settings.Default.ARCH_SSSE3,
                    ARCH_SSE4_1 = Settings.Default.ARCH_SSE4_1,
                    ARCH_SSE4_2 = Settings.Default.ARCH_SSE4_2,
                    ARCH_SSE4A = Settings.Default.ARCH_SSE4A,
                    ARCH_SSE5 = Settings.Default.ARCH_SSE5,
                    ARCH_AVX = Settings.Default.ARCH_AVX,
                    ARCH_AVX2 = Settings.Default.ARCH_AVX2,
                    ARCH_AVX512_VL = Settings.Default.ARCH_AVX512_VL,
                    ARCH_AVX512_PF = Settings.Default.ARCH_AVX512_PF,
                    ARCH_AVX512_DQ = Settings.Default.ARCH_AVX512_DQ,
                    ARCH_AVX512_BW = Settings.Default.ARCH_AVX512_BW,
                    ARCH_AVX512_ER = Settings.Default.ARCH_AVX512_ER,
                    ARCH_AVX512_F = Settings.Default.ARCH_AVX512_F,
                    ARCH_AVX512_CD = Settings.Default.ARCH_AVX512_CD,
                    ARCH_X64 = Settings.Default.ARCH_X64,
                    ARCH_BMI1 = Settings.Default.ARCH_BMI1,
                    ARCH_BMI2 = Settings.Default.ARCH_BMI2,
                    ARCH_P6 = Settings.Default.ARCH_P6,
                    ARCH_IA64 = Settings.Default.ARCH_IA64,
                    ARCH_FMA = Settings.Default.ARCH_FMA,
                    ARCH_TBM = Settings.Default.ARCH_TBM,
                    ARCH_AMD = Settings.Default.ARCH_AMD,
                    ARCH_PENT = Settings.Default.ARCH_PENT,
                    ARCH_3DNOW = Settings.Default.ARCH_3DNOW,
                    ARCH_CYRIX = Settings.Default.ARCH_CYRIX,
                    ARCH_CYRIXM = Settings.Default.ARCH_CYRIXM,
                    ARCH_VMX = Settings.Default.ARCH_VMX,
                    ARCH_RTM = Settings.Default.ARCH_RTM,
                    ARCH_MPX = Settings.Default.ARCH_MPX,
                    ARCH_SHA = Settings.Default.ARCH_SHA,
                    ARCH_BND = Settings.Default.ARCH_BND,
                    SignatureHelp_On = Settings.Default.SignatureHelp_On,
                    ARCH_ADX = Settings.Default.ARCH_ADX,
                    ARCH_F16C = Settings.Default.ARCH_F16C,
                    ARCH_FSGSBASE = Settings.Default.ARCH_FSGSBASE,
                    ARCH_HLE = Settings.Default.ARCH_HLE,
                    ARCH_INVPCID = Settings.Default.ARCH_INVPCID,
                    ARCH_PCLMULQDQ = Settings.Default.ARCH_PCLMULQDQ,
                    ARCH_LZCNT = Settings.Default.ARCH_LZCNT,
                    ARCH_PREFETCHWT1 = Settings.Default.ARCH_PREFETCHWT1,
                    ARCH_RDPID = Settings.Default.ARCH_RDPID,
                    ARCH_RDRAND = Settings.Default.ARCH_RDRAND,
                    ARCH_RDSEED = Settings.Default.ARCH_RDSEED,
                    ARCH_XSAVEOPT = Settings.Default.ARCH_XSAVEOPT,
                    ARCH_UNDOC = Settings.Default.ARCH_UNDOC,
                    ARCH_AES = Settings.Default.ARCH_AES,
                    IntelliSense_Show_Undefined_Includes = Settings.Default.IntelliSense_Show_Undefined_Includes,
                    PerformanceInfo_SandyBridge_On = Settings.Default.PerformanceInfo_SandyBridge_On,
                    PerformanceInfo_IvyBridge_On = Settings.Default.PerformanceInfo_IvyBridge_On,
                    PerformanceInfo_Haswell_On = Settings.Default.PerformanceInfo_Haswell_On,
                    PerformanceInfo_Broadwell_On = Settings.Default.PerformanceInfo_Broadwell_On,
                    PerformanceInfo_Skylake_On = Settings.Default.PerformanceInfo_Skylake_On,
                    PerformanceInfo_KnightsLanding_On = Settings.Default.PerformanceInfo_KnightsLanding_On,
                    AsmSim_On = Settings.Default.AsmSim_On,
                    AsmSim_Show_Syntax_Errors = Settings.Default.AsmSim_Show_Syntax_Errors,
                    AsmSim_Decorate_Syntax_Errors = Settings.Default.AsmSim_Decorate_Syntax_Errors,
                    AsmSim_Show_Usage_Of_Undefined = Settings.Default.AsmSim_Show_Usage_Of_Undefined,
                    AsmSim_Decorate_Usage_Of_Undefined = Settings.Default.AsmSim_Decorate_Usage_Of_Undefined,
                    AsmSim_Decorate_Registers = Settings.Default.AsmSim_Decorate_Registers,
                    AsmSim_Show_Register_In_Code_Completion = Settings.Default.AsmSim_Show_Register_In_Code_Completion,
                    AsmSim_Decorate_Unimplemented = Settings.Default.AsmSim_Decorate_Unimplemented,
                    IntelliSense_Decorate_Undefined_Includes = Settings.Default.IntelliSense_Decorate_Undefined_Includes,
                    ARCH_AVX512_IFMA = Settings.Default.ARCH_AVX512_IFMA,
                    ARCH_AVX512_VBMI = Settings.Default.ARCH_AVX512_VBMI,
                    ARCH_AVX512_VPOPCNTDQ = Settings.Default.ARCH_AVX512_VPOPCNTDQ,
                    ARCH_AVX512_4VNNIW = Settings.Default.ARCH_AVX512_4VNNIW,
                    ARCH_AVX512_4FMAPS = Settings.Default.ARCH_AVX512_4FMAPS,
                    AsmSim_64_Bits = Settings.Default.AsmSim_64_Bits,
                    SyntaxHighlighting_Userdefined1 = Settings.Default.SyntaxHighlighting_Userdefined1,
                    SyntaxHighlighting_Userdefined2 = Settings.Default.SyntaxHighlighting_Userdefined2,
                    SyntaxHighlighting_Userdefined3 = Settings.Default.SyntaxHighlighting_Userdefined3,
                    AsmSim_Z3_Timeout_MS = Settings.Default.AsmSim_Z3_Timeout_MS,
                    AsmSim_Show_Redundant_Instructions = Settings.Default.AsmSim_Show_Redundant_Instructions,
                    AsmSim_Decorate_Redundant_Instructions = Settings.Default.AsmSim_Decorate_Redundant_Instructions,
                    SyntaxHighlighting_Opcode_Italic = Settings.Default.SyntaxHighlighting_Opcode_Italic,
                    SyntaxHighlighting_Register_Italic = Settings.Default.SyntaxHighlighting_Register_Italic,
                    SyntaxHighlighting_Remark_Italic = Settings.Default.SyntaxHighlighting_Remark_Italic,
                    SyntaxHighlighting_Directive_Italic = Settings.Default.SyntaxHighlighting_Directive_Italic,
                    SyntaxHighlighting_Constant_Italic = Settings.Default.SyntaxHighlighting_Constant_Italic,
                    SyntaxHighlighting_Jump_Italic = Settings.Default.SyntaxHighlighting_Jump_Italic,
                    SyntaxHighlighting_Label_Italic = Settings.Default.SyntaxHighlighting_Label_Italic,
                    SyntaxHighlighting_Misc_Italic = Settings.Default.SyntaxHighlighting_Misc_Italic,
                    SyntaxHighlighting_Userdefined1_Italic = Settings.Default.SyntaxHighlighting_Userdefined1_Italic,
                    SyntaxHighlighting_Userdefined2_Italic = Settings.Default.SyntaxHighlighting_Userdefined2_Italic,
                    SyntaxHighlighting_Userdefined3_Italic = Settings.Default.SyntaxHighlighting_Userdefined3_Italic,
                    IntelliSense_Label_Analysis_On = Settings.Default.IntelliSense_Label_Analysis_On,
                    useAssemblerNasm_Att = Settings.Default.useAssemblerNasm_Att,
                    AsmSim_Number_Of_Threads = Settings.Default.AsmSim_Number_Of_Threads,
                    AsmSim_Show_Unreachable_Instructions = Settings.Default.AsmSim_Show_Unreachable_Instructions,
                    AsmSim_Decorate_Unreachable_Instructions = Settings.Default.AsmSim_Decorate_Unreachable_Instructions,
                    AsmSim_Pragma_Assume = Settings.Default.AsmSim_Pragma_Assume,
                    AsmSim_Show_Register_In_Instruction_Tooltip = Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip,
                    AsmSim_Show_Register_In_Register_Tooltip = Settings.Default.AsmSim_Show_Register_In_Register_Tooltip,
                    AsmSim_Show_Register_In_Code_Completion_Numeration = Settings.Default.AsmSim_Show_Register_In_Code_Completion_Numeration,
                    AsmSim_Show_Register_In_Instruction_Tooltip_Numeration = Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration,
                    AsmSim_Show_Register_In_Register_Tooltip_Numeration = Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration,
                    ARCH_AVX512_VBMI2 = Settings.Default.ARCH_AVX512_VBMI2,
                    ARCH_AVX512_VNNI = Settings.Default.ARCH_AVX512_VNNI,
                    ARCH_AVX512_BITALG = Settings.Default.ARCH_AVX512_BITALG,
                    ARCH_AVX512_GFNI = Settings.Default.ARCH_AVX512_GFNI,
                    ARCH_AVX512_VAES = Settings.Default.ARCH_AVX512_VAES,
                    ARCH_AVX512_VPCLMULQDQ = Settings.Default.ARCH_AVX512_VPCLMULQDQ,
                    ARCH_SMX = Settings.Default.ARCH_SMX,
                    ARCH_SGX1 = Settings.Default.ARCH_SGX1,
                    ARCH_SGX2 = Settings.Default.ARCH_SGX2,
                    PerformanceInfo_SkylakeX_On = Settings.Default.PerformanceInfo_SkylakeX_On,
                    PerformanceInfo_On = Settings.Default.PerformanceInfo_On,
                    ARCH_CLDEMOTE = Settings.Default.ARCH_CLDEMOTE,
                    ARCH_MOVDIR64B = Settings.Default.ARCH_MOVDIR64B,
                    ARCH_MOVDIRI = Settings.Default.ARCH_MOVDIRI,
                    ARCH_PCONFIG = Settings.Default.ARCH_PCONFIG,
                    ARCH_WAITPKG = Settings.Default.ARCH_WAITPKG,
                    ARCH_PRFCHW = Settings.Default.ARCH_PRFCHW,
                    ARCH_AVX512_BF16 = Settings.Default.ARCH_AVX512_BF16,
                    ARCH_AVX512_VP2INTERSECT = Settings.Default.ARCH_AVX512_VP2INTERSECT,
                    ARCH_ENQCMD = Settings.Default.ARCH_ENQCMD,
                    useAssemblerDisassemblyMasm = Settings.Default.useAssemblerDisassemblyMasm,
                    useAssemblerDisassemblyNasm_Att = Settings.Default.useAssemblerDisassemblyNasm_Att,
                    useAssemblerDisassemblyAutoDetect = Settings.Default.useAssemblerDisassemblyAutoDetect,
                    useAssemblerAutoDetect = Settings.Default.useAssemblerAutoDetect,
                    Global_MaxFileLines = Settings.Default.Global_MaxFileLines,
                };
            }
        }
        
        public IEnumerable<string> FilesToWatch => null;

        public object CustomMessageTarget => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            string lspPath = ApplicationInformation.LspPath();
            if (!File.Exists(lspPath))
            {
                string title = "Microsoft Visual Studio";
                string text = $"AsmDude2 could not find the Language Server Protocol (LSP)\nat the expected place: \"{lspPath}\"";
                MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: starting Language Server (LSP) " + lspPath);

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = lspPath,
                WorkingDirectory = Path.GetDirectoryName(lspPath)
            };

            const string stdInPipeName = @"output";
            const string stdOutPipeName = @"input";

            SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null); 
            PipeAccessRule pipeAccessRule = new PipeAccessRule(everyone, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            PipeSecurity pipeSecurity = new PipeSecurity();
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

            {
                string title = "Microsoft Visual Studio";
                string text = $"AsmDude2 could not start the Language Server Protocol (LSP)\nfound at \"{lspPath}\"";
                MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
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

        public async Task RestartServerAsync()
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: RestartServerAsync");
            await StopServerAsync();
            await OnLoadedAsync();
        }

        public Task OnServerInitializedAsync()
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnServerInitializedAsync");
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            AsmDudeToolsStatic.Output_INFO("AsmLanguageClient: OnServerInitializeFailedAsync");
            string message = $"Asm Language Client failed to activate\npath \"{ApplicationInformation.LspPath()}\"\n";
            string exception = initializationState.InitializationException?.ToString() ?? string.Empty;

            var failureContext = new InitializationFailureContext()
            {
                FailureMessage = $"{message}\n {exception}",
            };
            {
                string title = "Microsoft Visual Studio";
                string text = $"AsmDude2 Language Server Protocol (LSP) failed to activate.\n{exception.Substring(0, Math.Min(exception.Length, 1000))}...";
                MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return Task.FromResult(failureContext);
        }

        public object MiddleLayer => DiagnosticsFilterMiddleLayer.Instance;

        internal class DiagnosticsFilterMiddleLayer : ILanguageClientMiddleLayer
        {
            internal readonly static DiagnosticsFilterMiddleLayer Instance = new DiagnosticsFilterMiddleLayer();

            private DiagnosticsFilterMiddleLayer() { }

            public bool CanHandle(string methodName)
            {
                return true;
            }

            public async Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
            {
                AsmDudeToolsStatic.Output_INFO($"Received a LSP notification: name={methodName}; param={methodParam}");
                await sendNotification(methodParam);
            }

            public async Task<JToken> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken>> sendRequest)
            {
                AsmDudeToolsStatic.Output_INFO($"Received a LSP request: name={methodName}; param={methodParam}");
                return await sendRequest(methodParam);
            }
        }
    }
}
