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

using AsmTools;

using LanguageServerLibrary;

using Microsoft.VisualStudio.LanguageServer.Protocol;

using Newtonsoft.Json.Linq;

using StreamJsonRpc;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.UI.WebControls;

using static Nerdbank.Streams.MultiplexingStream;

namespace LanguageServer
{
    public sealed class CompletionComparer : IComparer<CompletionItem>
    {
        public int Compare(CompletionItem x, CompletionItem y)
        {
            Contract.Requires(x != null);
            Contract.Requires(y != null);
            return string.CompareOrdinal(x.SortText, y.SortText);
        }
    }

    public class LanguageServerTarget
    {
        private const int MAX_LENGTH_DESCR_TEXT = 120;
        private const int MaxNumberOfCharsInToolTips = 150;
        public static readonly CultureInfo CultureUI = CultureInfo.CurrentUICulture;


        private int version = 1;
        private readonly LanguageServer server;
        private readonly TraceSource traceSource;
        private AsmDude2Options options;
        private MnemonicStore mnemonicStore;
        private PerformanceStore performanceStore;

        private int completionItemsNumberRoot = 0;


        public LanguageServerTarget(LanguageServer server, TraceSource traceSource)
        {
            this.server = server;
            this.traceSource = traceSource;
        }

        public event EventHandler OnInitializeCompletion;

        public event EventHandler OnInitialized;

        public bool SuggestionMode
        {
            get;
            set;
        } = false;

        public bool IsIncomplete
        {
            get;
            set;
        } = false;

        public bool CompletionServerError
        {
            get;
            set;
        } = false;

        public bool ServerCommitCharacters { get; internal set; } = true;
        public bool ItemCommitCharacters { get; internal set; } = false;

        private void LogInfo(string message)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, message);
        }

        private static string Truncate(string text)
        {
            if (text.Length < MAX_LENGTH_DESCR_TEXT)
            {
                return text;
            }
            return text.Substring(0, MAX_LENGTH_DESCR_TEXT) + "...";
        }

        private (int, int) FindWordBoundary(int position, string lineStr)
        {
            int lineLength = lineStr.Length;
            if (position >= lineLength)
            {
                return (-1, -1);
            }

            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineStr[position]))
            {
                return (-1, -1);
            }
            int startPos = 0;
            int endPos = lineStr.Length;
            {
                var lineChars = lineStr.ToCharArray(0, lineStr.Length);
                for (int i = position; i < lineStr.Length; ++i)
                {
                    if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
                    {
                        endPos = i;
                        break;
                    }
                }
                for (int i = position; i >= 0; --i)
                {
                    if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
                    {
                        startPos = i + 1;
                        break;
                    }
                }
            }
            return (startPos, endPos);
        }

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            LogInfo($"Initialize: Received: {arg}");
            var parameter = arg.ToObject<InitializeParams>();

            if (parameter.InitializationOptions == null)
            {
                LogInfo($"Initialize: InitializationOptions is null");
            } else
            {
                JToken jToken = parameter.InitializationOptions as JToken;
                //LogInfo($"Initialize: Options: {jToken}");
                this.options = jToken.ToObject<AsmDude2Options>();
                this.server.options = this.options;
            }

            var result = new InitializeResult
            {
                Capabilities = new VSServerCapabilities
                {
                    TextDocumentSync = new TextDocumentSyncOptions
                    {
                        OpenClose = true,
                        Change = TextDocumentSyncKind.Full,
                    },
                    CompletionProvider = new CompletionOptions
                    {
                        TriggerCharacters = new string[] { },
                        AllCommitCharacters = new string[] { "\t" },
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { " ", "," },
                        RetriggerCharacters = new string[] { "," },                       
                        WorkDoneProgress = false,
                    },
                    RenameProvider = new RenameOptions
                    {
                        PrepareProvider = true,
                        WorkDoneProgress = false,
                    },
                    FoldingRangeProvider = new FoldingRangeOptions
                    {
                        WorkDoneProgress = false,
                    },
                    ReferencesProvider = new ReferenceOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DocumentHighlightProvider = new DocumentHighlightOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DocumentSymbolProvider = true,
                    CodeActionProvider = new CodeActionOptions()
                    {
                        ResolveProvider = true
                    },
                    ProjectContextProvider = true,
                    HoverProvider = new HoverOptions
                    {
                        WorkDoneProgress = false,
                    },
                    DefinitionProvider = false,
                    TypeDefinitionProvider = false,
                    ImplementationProvider = false,
                }
            };

            OnInitializeCompletion?.Invoke(this, EventArgs.Empty);

            LogInfo($"Initialize: Sent: {JToken.FromObject(result)}");

            return result;
        }

        [JsonRpcMethod(Methods.InitializedName)]
        public void Initialized(JToken arg)
        {
            LogInfo($"Initialized: Received: {arg}");

            #region load Signature Store and Performance Store
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "Resources");
            {
                string filename_Regular = Path.Combine(path, "signature-may2019.txt");
                string filename_Hand = Path.Combine(path, "signature-hand-1.txt");
                this.mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand, this.traceSource, this.options);
            }
            
            {
                string path_performance = Path.Combine(path, "Performance");
                this.performanceStore = new PerformanceStore(path_performance, this.traceSource, this.options);
            }
            #endregion

            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            LogInfo($"OnTextDocumentOpened: Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover OnHover(JToken arg)
        {
            LogInfo($"OnHover: Received: {arg}");
            //LogInfo($"OnHover: maxProblems={this.server.maxProblems}");
            //this.server.ShowMessage($"OnHover maxProblems={this.server.maxProblems}\"", MessageType.Error);

            var parameter = arg.ToObject<TextDocumentPositionParams>();
            string lineStr = this.server.GetLine(parameter.Position.Line, parameter.TextDocument);
            (int startPos, int endPos) = FindWordBoundary(parameter.Position.Character, lineStr);
            int length = endPos - startPos;

            //LogInfo($"OnHover: lineStr=\"{lineStr}\"; startPos={startPos}; endPos={endPos}");

            if (length <= 0)
            {
                return null;
            }
            string keyword = lineStr.Substring(startPos, length).ToUpperInvariant();
            string keyword_upcase = keyword;
            string full_Descr = $"No match: keyword=\"{keyword}\" maxProblems={this.server.maxProblems}";
            string performanceStr = "No performance info";

            //LogInfo($"OnHover: keyword={keyword}");

            if (AsmTools.AsmSourceTools.IsMnemonic(keyword_upcase, true))
            {
                Mnemonic mnemonic = AsmTools.AsmSourceTools.ParseMnemonic(keyword_upcase, true);
                string mnemonicStr = mnemonic.ToString();
                string archStr = ":" + ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));
                string descr = this.mnemonicStore.GetDescription(mnemonic);
                full_Descr = AsmTools.AsmSourceTools.Linewrap($"{mnemonicStr} {archStr} {descr}", MaxNumberOfCharsInToolTips);

                //if (AsmDude.Settings.Default.PerformanceInfo_On)
                if (true)
                {
                    bool first = true;
                    string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                    MicroArch selectedMicroArchs = MicroArch.SkylakeX | MicroArch.Haswell;// AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                    foreach (PerformanceItem item in this.performanceStore.GetPerformance(mnemonic, selectedMicroArchs))
                    {
                        if (first)
                        {
                            first = false;

                            string msg1 = string.Format(
                                CultureUI,
                                format,
                                string.Empty, string.Empty, "µOps", "µOps", "µOps", string.Empty, string.Empty, string.Empty);

                            string msg2 = string.Format(
                                CultureUI,
                                "\n" + format,
                                "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", string.Empty);

                            performanceStr = msg1;
                            performanceStr += msg2;
                        }

                        string msg3 = string.Format(
                            CultureUI,
                            "\n" + format,
                            item.microArch_ + " ",
                            item.instr_ + " " + item.args_ + " ",
                            item.mu_Ops_Fused_ + " ",
                            item.mu_Ops_Merged_ + " ",
                            item.mu_Ops_Port_ + " ",
                            item.latency_ + " ",
                            item.throughput_ + " ",
                            item.remark_);

                        performanceStr += msg3;
                    }
                }
            }

            /*
            switch (tag.Type)
            {
                case AsmTokenType.Misc: // intentional fall through
                case AsmTokenType.Directive:
                    {
                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }
                            descr = AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);

                            var containerElement = new ContainerElement(
                                 ContainerElementStyle.Wrapped,
                                 new ImageElement(_icon),
                                 new ClassifiedTextElement(
                                     new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Directive "),
                                     new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, keyword),
                                     new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, descr)));

                            return (new List<object> { containerElement }, keywordSpan.Value);
                        }
                        break;
                    }
                case AsmTokenType.Register:
                    {
                        int lineNumber = AsmDudeToolsStatic.Get_LineNumber(tagSpan);
                        if (keyword_upcase.StartsWith("%", StringComparison.Ordinal))
                        {
                            keyword_upcase = keyword_upcase.Substring(1); // remove the preceding % in AT&T syntax
                        }

                        Rn reg = RegisterTools.ParseRn(keyword_upcase, true);
                        if (this.asmDudeTools_.RegisterSwitchedOn(reg))
                        {
                            string regStr = reg.ToString();
                            Arch arch = RegisterTools.GetArch(reg);

                            string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "] ";
                            string descr = this.asmDudeTools_.Get_Description(regStr);
                            if (regStr.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }
                            string full_Descr = AsmSourceTools.Linewrap(":" + archStr + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);

                            var containerElement = new ContainerElement(
                                ContainerElementStyle.Wrapped,
                                new ImageElement(_icon),
                                new ClassifiedTextElement(
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Register "),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, regStr),
                                    new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, full_Descr)));

                            return (new List<object> { containerElement }, keywordSpan.Value);
                        }
                        break;
                    }
                case AsmTokenType.Mnemonic: // intentional fall through
                case AsmTokenType.MnemonicOff: // intentional fall through
                case AsmTokenType.Jump:
                    { // done....
                    }
                case AsmTokenType.Label:
                    {
                        string label = keyword;
                        string labelPrefix = tag.Misc;
                        string full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(labelPrefix, label, AsmDudeToolsStatic.Used_Assembler);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

                        string descr = this.Get_Label_Description(full_Qualified_Label);
                        if (descr.Length == 0)
                        {
                            descr = this.Get_Label_Description(label);
                        }
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.LabelDef:
                    {
                        string label = keyword;
                        string extra_Tag_Info = tag.Misc;
                        string full_Qualified_Label;
                        if ((extra_Tag_Info != null) && extra_Tag_Info.Equals(AsmTokenTag.MISC_KEYWORD_PROTO, StringComparison.Ordinal))
                        {
                            full_Qualified_Label = label;
                        }
                        else
                        {
                            full_Qualified_Label = AsmDudeToolsStatic.Make_Full_Qualified_Label(extra_Tag_Info, label, AsmDudeToolsStatic.Used_Assembler);
                        }

                        AsmDudeToolsStatic.Output_INFO("AsmQuickInfoSource:AugmentQuickInfoSession: found label def " + full_Qualified_Label);

                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("Label ", foreground));
                        description.Inlines.Add(Make_Run2(full_Qualified_Label, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Label))));

                        string descr = this.Get_Label_Def_Description(full_Qualified_Label, label);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.Constant:
                    {
                        (bool valid, ulong value, int nBits) = AsmSourceTools.Evaluate_Constant(keyword);
                        string constantStr = valid
                            ? value + "d = " + value.ToString("X", AsmDudeToolsStatic.CultureUI) + "h = " + AsmSourceTools.ToStringBin(value, nBits) + "b"
                            : keyword;

                        var containerElement = new ContainerElement(
                            ContainerElementStyle.Wrapped,
                            new ImageElement(_icon),
                            new ClassifiedTextElement(
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "Constant "),
                                new ClassifiedTextRun(PredefinedClassificationTypeNames.Number, constantStr)));

                        return (new List<object> { containerElement }, keywordSpan.Value);
                    }
                case AsmTokenType.UserDefined1:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 1: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined1))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.UserDefined2:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 2: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined2))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                case AsmTokenType.UserDefined3:
                    {
                        description = new TextBlock();
                        description.Inlines.Add(Make_Run1("User defined 3: ", foreground));
                        description.Inlines.Add(Make_Run2(keyword, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmDude.Settings.Default.SyntaxHighlighting_Userdefined3))));

                        string descr = this.asmDudeTools_.Get_Description(keyword_upcase);
                        if (descr.Length > 0)
                        {
                            if (keyword.Length > (AsmDudePackage.MaxNumberOfCharsInToolTips / 2))
                            {
                                descr = "\n" + descr;
                            }

                            description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + descr, AsmDudePackage.MaxNumberOfCharsInToolTips))
                            {
                                Foreground = foreground,
                            });
                        }
                        break;
                    }
                default:
                    //description = new TextBlock();
                    //description.Inlines.Add(makeRun1("Unused tagType " + asmTokenTag.Tag.type));
                    break;
            }
            if (description != null)
            {
                description.Focusable = true;
                description.FontSize = AsmDudeToolsStatic.GetFontSize() + 2;
                description.FontFamily = AsmDudeToolsStatic.GetFontType();
                //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:AugmentQuickInfoSession; setting description fontSize={1}; fontFamily={2}", this.ToString(), description.FontSize, description.FontFamily));
                //quickInfoContent.Add(description);
                return (new List<object> { "other" }, keywordSpan.Value);
            }
            */


            //LogInfo($"OnHover: keyword = {keyword}; full_Descr={full_Descr}");


            var hoverContent = new SumType<string, MarkedString>[]{
                new SumType<string, MarkedString>(new MarkedString
                {
                    Language = MarkupKind.PlainText.ToString(),
                    Value = full_Descr + "\n",
                }),
                new SumType<string, MarkedString>(new MarkedString
                {
                    Language = MarkupKind.Markdown.ToString(),
                    Value = "**Performance:**\n",
                }),
                new SumType<string, MarkedString>(new MarkedString
                {
                    Language = MarkupKind.Markdown.ToString(),
                    Value = "```text\n" + performanceStr + "\n```",
                })
            };


            var result = new Hover()
            {
                //     Gets or sets the content for the hover. Object can either be an array or a single
                //     object. If the object is an array the array can contain objects of type Microsoft.VisualStudio.LanguageServer.Protocol.MarkedString
                //     and System.String. If the object is not an array it can be of type Microsoft.VisualStudio.LanguageServer.Protocol.MarkedString,
                //     System.String, or Microsoft.VisualStudio.LanguageServer.Protocol.MarkupContent.
                // Contents = new SumType<string, MarkedString>(full_Descr + "\n" + performanceStr),


                //Contents = new MarkupContent
                //{
                //    Kind = MarkupKind.Markdown,
                //    Value = full_Descr + "\n*BOLD*\n```\n" + performanceStr + "\n```\n__Underscore__",
                //},

                //Contents = new SumType<string, MarkedString>(new MarkedString
                //{
                //    Language = MarkupKind.Markdown.ToString(),
                //    Value = full_Descr + "\n```typescript\n" + performanceStr + "\n```",
                //}),

                Contents = hoverContent,
                Range = new Range()
                {
                    Start = new Position(parameter.Position.Line, startPos),
                    End = new Position(parameter.Position.Line, endPos),
                },
            };

            LogInfo($"OnHover: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            LogInfo($"OnTextDocumentChanged: Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");
            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version);
            server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            LogInfo($"OnTextDocumentClosed: Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            LogInfo($"OnTextDocumentFindReferences: Received: {JToken.FromObject(parameter)}");
            var result = server.SendReferences(parameter, returnLocationsOnly: true, token: token);
            LogInfo($"OnTextDocumentFindReferences: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object GetCodeActions(JToken arg)
        {
            LogInfo($"GetCodeActions: Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = server.GetCodeActions(parameter);
            LogInfo($"GetCodeActions: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName)]
        public object GetResolvedCodeAction(JToken arg)
        {
            LogInfo($"GetResolvedCodeAction: Received: {arg}");
            var parameter = arg.ToObject<CodeAction>();
            var result = server.GetResolvedCodeAction(parameter);
            LogInfo($"GetResolvedCodeAction: Sent: {JToken.FromObject(result)}");
            return result;
        }

        private IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, ISet<AsmTokenType> selectedTypes, bool addSpecialKeywords)
        {
            SortedSet<CompletionItem> completions = new SortedSet<CompletionItem>(new CompletionComparer());

            //Add the completions of AsmDude directives (such as code folding directives)
            #region
            bool codeFoldingOn = true; //Settings.Default.CodeFolding_On
            if (addSpecialKeywords && codeFoldingOn)
            {
                {
                    string labelText = "#region"; // Settings.Default.CodeFolding_BeginTag;     //the characters that start the outlining region
                    completions.Add(new CompletionItem
                    {
                        Kind = CompletionItemKind.Keyword,
                        Label = labelText,
                        InsertText = labelText,
                        SortText = labelText,
                        Documentation = $"{labelText} - keyword to start code folding",
                    });
                }
                {
                    string labelText = "#endregion"; // Settings.Default.CodeFolding_EndTag;       //the characters that end the outlining region
                    completions.Add(new CompletionItem
                    {
                        Kind = CompletionItemKind.Keyword,
                        Label = labelText,
                        InsertText = labelText,
                        SortText = labelText,
                        Documentation = $"{labelText} - keyword to end code folding",
                });
                }
            }
            #endregion

            // AssemblerEnum usedAssember = AssemblerEnum.NASM_INTEL; //AsmDudeToolsStatic.Used_Assembler;

            #region Add completions
            if (true)
            {
                if (true) 
                //if (selectedTypes.Contains(AsmDude2.AsmTokenType.Mnemonic))
                {
                    //foreach (Mnemonic mnemonic in this.asmDudeTools_.Get_Allowed_Mnemonics())
                    foreach (Mnemonic mnemonic in Enum.GetValues(typeof(Mnemonic)))
                    {
                        string keyword_upcase = mnemonic.ToString();
                        string insertionText = useCapitals ? keyword_upcase : keyword_upcase.ToLowerInvariant();
                        string archStr = ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));

                        completions.Add(new CompletionItem
                        {
                            Kind = CompletionItemKind.Keyword,
                            Label = $"{keyword_upcase} {archStr}",
                            InsertText = insertionText,
                            SortText = insertionText,
                            Documentation = this.mnemonicStore.GetDescription(mnemonic),
                        });
                    }
                }
            }
            /*
            //Add the completions that are defined in the xml file
            foreach (string keyword_upcase in this.asmDudeTools_.Get_Keywords())
            {
                AsmTokenType type = this.asmDudeTools_.Get_Token_Type_Intel(keyword_upcase);
                if (selectedTypes.Contains(type))
                {
                    Arch arch = Arch.ARCH_NONE;
                    bool selected = true;

                    if (type == AsmTokenType.Directive)
                    {
                        AssemblerEnum assembler = this.asmDudeTools_.Get_Assembler(keyword_upcase);
                        if (assembler.HasFlag(AssemblerEnum.MASM))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.MASM))
                            {
                                selected = false;
                            }
                        }
                        else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL) || assembler.HasFlag(AssemblerEnum.NASM_ATT))
                        {
                            if (!usedAssember.HasFlag(AssemblerEnum.NASM_INTEL))
                            {
                                selected = false;
                            }
                        }
                    }
                    else
                    {
                        arch = this.asmDudeTools_.Get_Architecture(keyword_upcase);
                        selected = AsmDudeToolsStatic.Is_Arch_Switched_On(arch);
                    }

                    //AsmDudeToolsStatic.Output_INFO("CodeCompletionSource:Selected_Completions; keyword=" + keyword + "; arch=" + arch + "; selected=" + selected);

                    if (selected)
                    {
                        //Debug.WriteLine("INFO: CompletionSource:AugmentCompletionSession: name keyword \"" + entry.Key + "\"");

                        // by default, the entry.Key is with capitals
                        string insertionText = useCapitals ? keyword_upcase : keyword_upcase.ToLowerInvariant();
                        string archStr = (arch == Arch.ARCH_NONE) ? string.Empty : " [" + ArchTools.ToString(arch) + "]";
                        string descriptionStr = this.asmDudeTools_.Get_Description(keyword_upcase);
                        descriptionStr = (string.IsNullOrEmpty(descriptionStr)) ? string.Empty : " - " + descriptionStr;
                        string displayTextFull = keyword_upcase + archStr + descriptionStr;
                        string displayText = Truncate(displayTextFull);
                        //String description = keyword.PadRight(15) + archStr.PadLeft(8) + descriptionStr;
                        this.icons_.TryGetValue(type, out ImageSource imageSource);
                        completions.Add(new Completion(displayText, insertionText, displayTextFull, imageSource, string.Empty));
                    }
                }
            }
            */
            #endregion

            return completions;
        }

        [JsonRpcMethod(Methods.TextDocumentCompletionName)]
        public CompletionList OnTextDocumentCompletion(JToken arg)
        {
            LogInfo($"OnTextDocumentCompletion: Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();
            //LogInfo($"OnTextDocumentCompletion: parameter: {parameter}");
            string lineStr = this.server.GetLine(parameter.Position.Line, parameter.TextDocument);
            //LogInfo($"OnTextDocumentCompletion: lineStr: \"{lineStr}\"");

            int pos = parameter.Position.Character - 1;
            if (pos >= lineStr.Length)
            {
                LogInfo($"OnTextDocumentCompletion: char position is too large: pos={pos}; lineStr.Length={lineStr.Length}");
                return null;
            }
            char currentChar = lineStr[pos];
            //LogInfo($"OnTextDocumentCompletion: currentChar={currentChar}");

            if (AsmTools.AsmSourceTools.IsSeparatorChar(currentChar))
            {
                return null;
            }
            (string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr);

            LogInfo($"OnTextDocumentCompletion: label={label}; mnemonic={mnemonic}; args={args}; remark={remark}");

            List<CompletionItem> items = new List<CompletionItem>();

            if (mnemonic == Mnemonic.NONE) {
                ISet<AsmTokenType> selected1 = new HashSet<AsmTokenType> { AsmTokenType.Directive, AsmTokenType.Jump, AsmTokenType.Misc, AsmTokenType.Mnemonic };
                bool useCapitals = true;
                items.AddRange(this.Selected_Completions(useCapitals, selected1, true));
            }

#pragma warning disable CS0162 // Unreachable code detected
            if (false)
            {
                this.server.LastCompletionRequest = arg.ToString();
                var allKinds = Enum.GetValues(typeof(CompletionItemKind)) as CompletionItemKind[];
                var itemName = this.IsIncomplete ? "Incomplete" : "Item";

                for (int i = 0; i < 10; i++)
                {
                    var item = new CompletionItem();
                    item.Label = $"{itemName} {i + completionItemsNumberRoot}";
                    item.InsertText = $"{itemName}{i + completionItemsNumberRoot}";
                    item.SortText = item.Label;
                    item.Kind = allKinds[(completionItemsNumberRoot + i) % allKinds.Length];
                    item.Detail = $"Detail for {itemName} {i + completionItemsNumberRoot}";
                    item.Documentation = $"Documentation for {itemName} {i + completionItemsNumberRoot}";
                    item.CommitCharacters = null;

                    items.Add(item);
                }
                this.completionItemsNumberRoot += 10;
            }
            if (false)
            {
                {
                    items.Add(new CompletionItem
                    {
                        Kind = CompletionItemKind.Keyword,
                        Label = "VPXORD",
                        InsertText = "VPXORD",
                        SortText = "VPXORD",
                        //Detail = "detail text A",
                        Documentation = new MarkupContent
                        {
                            Kind = MarkupKind.Markdown,
                            Value = "# Header\nSome text 2\n```typescript\nsomeCode2();\n```",
                            //Value = "Documentation: some **text** here, and __here__ much texts here and here and \nhere and here and here",
                        },
                    });
                }
                {
                    var item = new CompletionItem();
                    item.Label = "VPXORQ";
                    item.InsertText = item.Label;
                    item.SortText = item.Label;
                    item.Kind = CompletionItemKind.Keyword;
                    item.Documentation = "Documentation: some text here B";
                    item.Detail = "Detail: B";
                    items.Add(item);
                }
            }
            if (false)
            {
                var fileNames = new[] { "sample.txt", "myHeader.h", "src/Feature/MyClass.cs", "../resources/img/sample.png", "http://contoso.com/awesome/Index.razor", "http://schemas.microsoft.com/winfx/2006/xaml/file.xml" };
                for (int i = 0; i < fileNames.Length; i++)
                {
                    var item = new CompletionItem();
                    item.Label = fileNames[i];
                    item.InsertText = fileNames[i];
                    item.SortText = fileNames[i];
                    item.Kind = CompletionItemKind.File;
                    item.Detail = "detail: bla";
                    item.Documentation = $"Documentation: Verifies whether IVsImageService provided correct icon for {fileNames[i]}";
                    item.CommitCharacters = new string[] { "." };
                    items.Add(item);
                }
            }
#pragma warning restore CS0162 // Unreachable code detected

            var list = new CompletionList()
            {
                IsIncomplete = this.IsIncomplete || (parameter.Context.TriggerKind == CompletionTriggerKind.TriggerForIncompleteCompletions),
                Items = items.ToArray(),
            };

            this.server.IsIncomplete = false;
            LogInfo($"OnTextDocumentCompletion: Sent: {JToken.FromObject(list)}");

            return list;
        }

        /// <summary>
        /// Constrain the list of signatures given: 1) the currently operands provided by the user; and 2) the selected architectures
        /// </summary>
        /// <param name="data"></param>
        /// <param name="operands"></param>
        /// <returns></returns>
        private static IEnumerable<AsmSignatureInformation> Constrain_Signatures(
                IEnumerable<AsmSignatureInformation> data,
                IList<Operand> operands,
                ISet<Arch> selectedArchitectures)
        {
            foreach (AsmSignatureInformation asmSignatureElement in data)
            {
                bool allowed = true;

                //1] constrain on architecture
                if (!asmSignatureElement.Is_Allowed(selectedArchitectures))
                {
                    allowed = false;
                }

                //2] constrain on operands
                if (allowed)
                {
                    if ((operands == null) || (operands.Count == 0))
                    {
                        // do nothing
                    }
                    else
                    {
                        for (int i = 0; i < operands.Count; ++i)
                        {
                            Operand operand = operands[i];
                            if (operand != null)
                            {
                                if (!asmSignatureElement.Is_Allowed(operand, i))
                                {
                                    allowed = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (allowed)
                {
                    yield return asmSignatureElement;
                }
            }
        }


        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp OnTextDocumentSignatureHelp(JToken arg)
        {
            LogInfo($"OnTextDocumentSignatureHelp: Received: {arg}");
            var parameter = arg.ToObject<SignatureHelpParams>();
            string lineStr = this.server.GetLine(parameter.Position.Line, parameter.TextDocument);
            (string label, Mnemonic mnemonic, string[] args, string remark) = AsmTools.AsmSourceTools.ParseLine(lineStr);
            IList<Operand> operands = AsmTools.AsmSourceTools.MakeOperands(args);

            ISet<Arch> selectedArchitectures = new HashSet<Arch> { }; // AsmDudeToolsStatic.Get_Arch_Switched_On();
            foreach (Arch arch in Enum.GetValues(typeof(Arch)))
            {
                selectedArchitectures.Add(arch);
            }
            LogInfo($"OnTextDocumentSignatureHelp: selected architectures={ArchTools.ToString(selectedArchitectures)}");

            var x = this.mnemonicStore.GetSignatures(mnemonic);
            var y = Constrain_Signatures(x, operands, selectedArchitectures);
            var z = new List<SignatureInformation>();
            foreach (AsmSignatureInformation asmSignatureElement in y)
            {
                LogInfo($"OnTextDocumentSignatureHelp: adding SignatureInformation: {asmSignatureElement.SignatureInformation.Label}");
                z.Add(asmSignatureElement.SignatureInformation);
            }

            // 1 find the keyword
            // 2 find the parameter (that is, count the comma's)
            // 3 select from the list of signatures, the ones that are applicable
            int nCommas = operands.Count;

            LogInfo($"OnTextDocumentSignatureHelp: args={args}");

            LogInfo($"OnTextDocumentSignatureHelp: lineStr\"{lineStr}\"; pos={parameter.Position.Character}; {mnemonic}, nCommas {nCommas}");
            SignatureHelp retVal = new SignatureHelp()
            {
                ActiveParameter = 1,
                ActiveSignature = nCommas,
                Signatures = z.ToArray<SignatureInformation>(),
                /*
                Signatures = new SignatureInformation[]
                {
                    new SignatureInformation()
                    {
                        Label = mnemonic.ToString(),
                        Parameters = new ParameterInformation[]
                        {
                            new ParameterInformation()
                            {
                                Label = "param1",
                                Documentation = "documentation here A",
                            },
                            new ParameterInformation()
                            {
                                Label = "param2",
                                Documentation = "documentation here B",
                            }
                        },
                        Documentation = "documentation here C",
                    },
                    new SignatureInformation()
                    {
                        Label = "foo(param1, param2, param3)",
                        Parameters = new ParameterInformation[]
                        {
                            new ParameterInformation()
                            {
                                Label = "param1"
                            },
                            new ParameterInformation()
                            {
                                Label = "param2"
                            },
                            new ParameterInformation()
                            {
                                Label = "param3"
                            }
                        },
                    }
                },
                */
            };

            LogInfo($"OnTextDocumentSignatureHelp: Sent: {JToken.FromObject(retVal)}");

            return retVal;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            LogInfo($"OnDidChangeConfiguration: Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
        {
            LogInfo($"Received Shutdown notification");
            return null;
        }

        [JsonRpcMethod(Methods.ExitName)]
        public void Exit()
        {
            LogInfo($"Received Exit notification");
            server.Exit();
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit Rename(JToken arg)
        {
            LogInfo($"Received: {arg}");
            var renameParams = arg.ToObject<RenameParams>();
            string fullText = File.ReadAllText(renameParams.TextDocument.Uri.LocalPath);
            string wordToReplace = GetWordAtPosition(fullText, renameParams.Position);
            Range[] placesToReplace = GetWordRangesInText(fullText, wordToReplace);

            var result = new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new TextDocumentEdit
                    {
                        TextDocument = new OptionalVersionedTextDocumentIdentifier
                        {
                            Uri = renameParams.TextDocument.Uri,
                            Version = ++version
                        },
                        Edits = placesToReplace.Select(range =>
                            new TextEdit
                            {
                                NewText = renameParams.NewName,
                                Range = range
                            }).ToArray()
                    }
                }
            };

            LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName)]
        public object GetFoldingRanges(JToken arg)
        {
            LogInfo($"GetFoldingRanges: Received: {arg}");
            return this.server.GetFoldingRanges();
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            LogInfo($"GetProjectContexts: Received: {arg}");
            var result = this.server.GetProjectContexts();
            LogInfo($"GetProjectContexts: Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName)]
        public object GetDocumentSymbols(JToken arg)
        {
            LogInfo($"GetDocumentSymbols: Received: {arg}");
            return this.server.GetDocumentSymbols();
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            LogInfo($"GetDocumentHighlights: Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, token);
            LogInfo($"GetDocumentHighlights: Sent: {JToken.FromObject(result)}");
            return result;
        }

        public Range[] GetWordRangesInText(string fullText, string word)
        {
            List<Range> ranges = new List<Range>();
            string[] textLines = fullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < textLines.Length; i++)
            {
                foreach (Match match in Regex.Matches(textLines[i], word))
                {
                    ranges.Add(new Range
                    {
                        Start = new Position(i, match.Index),
                        End = new Position(i, match.Index + match.Length)
                    });
                }
            }

            return ranges.ToArray();
        }

        public string GetWordAtPosition(string fullText, Position position)
        {
            string[] textLines = fullText.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            string textAtSpecifiedLine = textLines[position.Line];

            string currentWord = string.Empty;
            for (int i = position.Character; i < textAtSpecifiedLine.Length; i++)
            {
                if (textAtSpecifiedLine[i] == ' ')
                {
                    break;
                }
                else
                {
                    currentWord += textAtSpecifiedLine[i];
                }
            }

            for (int i = position.Character - 1; i > 0; i--)
            {
                if (textAtSpecifiedLine[i] == ' ')
                {
                    break;
                }
                else
                {
                    currentWord = textAtSpecifiedLine[i] + currentWord;
                }
            }

            return currentWord;
        }

        public string GetText()
        {
            return string.IsNullOrWhiteSpace(this.server.CustomText) ? "custom text from language server target" : this.server.CustomText;
        }
    }
}
