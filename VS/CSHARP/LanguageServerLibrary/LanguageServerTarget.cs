using AsmDude2.Tools;

using AsmTools;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;

using static QuikGraph.Algorithms.Assignment.HungarianAlgorithm;

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


        private int version = 1;
        private readonly LanguageServer server;
        private int completionItemsNumberRoot = 0;
        private readonly TraceSource traceSource;
        private MnemonicStore mnemonicStore;

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

        private readonly string[] ServerCommitCharacterArray = new[] { " ", "[", "]", ";" };
        private readonly string[] ItemCommitCharacterArray = new[] { " ", "[", "]", "(", ")", ";", "-" };

        private void LogInfo(string message)
        {
            this.traceSource.TraceEvent(TraceEventType.Information, 0, message);
        }

        [JsonRpcMethod(Methods.InitializeName)]
        public object Initialize(JToken arg)
        {
            this.LogInfo($"Received: {arg}");

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
                        TriggerCharacters = new string[] { ",", ".", "@" },
                        AllCommitCharacters = ServerCommitCharacterArray,
                        ResolveProvider = false,
                        WorkDoneProgress = false,
                    },
                    SignatureHelpProvider = new SignatureHelpOptions()
                    {
                        TriggerCharacters = new string[] { "(", "," },
                        RetriggerCharacters = new string[] { ")" },
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

            #region load Signature Store and Performance Store
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "Resources");
            {
                string filename_Regular = Path.Combine(path, "signature-may2019.txt");
                string filename_Hand = Path.Combine(path, "signature-may2019.txt");
                this.mnemonicStore = new MnemonicStore(filename_Regular, filename_Hand, this.traceSource);
            }
            {
                //string path_performance = Path.Combine(path, "Performance");
                //this.performanceStore_ = new PerformanceStore(path_performance);
            }
            #endregion

            OnInitializeCompletion?.Invoke(this, new EventArgs());

            this.LogInfo($"Sent: {JToken.FromObject(result)}");

            return result;
        }

        [JsonRpcMethod(Methods.InitializedName)]
        public void Initialized(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            this.OnInitialized?.Invoke(this, EventArgs.Empty);
        }

        [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
        public void OnTextDocumentOpened(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<DidOpenTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Open: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentOpened(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentHoverName)]
        public Hover OnHover(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<TextDocumentPositionParams>();
            string lineStr = this.server.GetLine(parameter.Position.Line, parameter.TextDocument);

            int startPos = 0;
            int endPos = lineStr.Length;
            {
                var lineChars = lineStr.ToCharArray(0, lineStr.Length);
                for (int i = parameter.Position.Character; i < lineStr.Length; ++i)
                {
                    if (AsmSourceTools.IsSeparatorChar(lineChars[i]))
                    {
                        endPos = i;
                        break;
                    }
                }
                for (int i = parameter.Position.Character; i >= 0; --i)
                {
                    if (AsmSourceTools.IsSeparatorChar(lineChars[i]))
                    {
                        startPos = i+1;
                        break;
                    }
                }
            }

            string keyword = lineStr.Substring(startPos, endPos - startPos);
            string keyword_upcase = keyword.ToUpperInvariant();
            string full_Descr = $"No match: keyword=\"{keyword}\"";

            if (AsmSourceTools.IsMnemonic(keyword_upcase, true))
            {
                Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword_upcase, true);
                string mnemonicStr = mnemonic.ToString();
                string archStr = ":" + ArchTools.ToString(this.mnemonicStore.GetArch(mnemonic));
                string descr = this.mnemonicStore.GetDescription(mnemonic);
                full_Descr = AsmSourceTools.Linewrap($"{mnemonicStr} {archStr} {descr}", MaxNumberOfCharsInToolTips);

                /*
                if (AsmDude.Settings.Default.PerformanceInfo_On)
                {
                    //this.PerformanceExpander.IsExpanded = !Settings.Default.PerformanceInfo_IsDefaultCollapsed; //TODO remove PerformanceInfo_IsDefaultCollapsed

                    bool empty = true;
                    bool first = true;
                    FontFamily family = new FontFamily("Consolas");
                    string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                    var performanceElements = new List<ClassifiedTextRun>();

                    MicroArch selectedMicroarchitures = AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                    foreach (PerformanceItem item in this.asmDudeTools_.Performance_Store.GetPerformance(mnemonic, selectedMicroarchitures))
                    {
                        empty = false;
                        if (first)
                        {
                            first = false;

                            string msg1 = string.Format(
                                AsmDudeToolsStatic.CultureUI,
                                format,
                                string.Empty, string.Empty, "µOps", "µOps", "µOps", string.Empty, string.Empty, string.Empty);

                            string msg2 = string.Format(
                                AsmDudeToolsStatic.CultureUI,
                                "\n" + format,
                                "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", string.Empty);

                            performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, "\n" + msg1, ClassifiedTextRunStyle.UseClassificationFont));
                            performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, msg2, ClassifiedTextRunStyle.UseClassificationFont));
                        }

                        string msg3 = string.Format(
                            AsmDudeToolsStatic.CultureUI,
                            "\n" + format,
                            item.microArch_ + " ",
                            item.instr_ + " " + item.args_ + " ",
                            item.mu_Ops_Fused_ + " ",
                            item.mu_Ops_Merged_ + " ",
                            item.mu_Ops_Port_ + " ",
                            item.latency_ + " ",
                            item.throughput_ + " ",
                            item.remark_);

                        performanceElements.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.NaturalLanguage, msg3, ClassifiedTextRunStyle.UseClassificationFont));
                    }

                    if (!empty)
                    {
                        x.Add(new ClassifiedTextElement(performanceElements));
                    }
                }
                var linkElement = ClassifiedTextElement.CreateHyperlink("More details", "Click here to see more details about this expression", () =>
                {
                    System.Diagnostics.Process.Start($"https://github.com/SnellerInc/asm-dude");
                });
                x.Add(linkElement);

                return (x, keywordSpan.Value);
                */
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


            this.LogInfo($"keyword = {keyword}; full_Descr={full_Descr}");

            var result = new Hover()
            {
                Contents = new SumType<string, MarkedString>(full_Descr),
                Range = new Range()
                {
                    Start = new Position(parameter.Position.Line, startPos),
                    End = new Position(parameter.Position.Line, endPos),
                },
            };

            this.LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
        public void OnTextDocumentChanged(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<DidChangeTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Change: {parameter.TextDocument.Uri.AbsolutePath}");
            server.UpdateServerSideTextDocument(parameter.ContentChanges[0].Text, parameter.TextDocument.Version);
            server.SendDiagnostics(parameter.TextDocument.Uri);
        }

        [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
        public void OnTextDocumentClosed(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<DidCloseTextDocumentParams>();
            System.Diagnostics.Debug.WriteLine($"Document Close: {parameter.TextDocument.Uri.AbsolutePath}");
            server.OnTextDocumentClosed(parameter);
        }

        [JsonRpcMethod(Methods.TextDocumentReferencesName, UseSingleObjectParameterDeserialization = true)]
        public object[] OnTextDocumentFindReferences(ReferenceParams parameter, CancellationToken token)
        {
            this.LogInfo($"Received: {JToken.FromObject(parameter)}");
            var result = server.SendReferences(parameter, returnLocationsOnly: true, token: token);
            this.LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentCodeActionName)]
        public object GetCodeActions(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<CodeActionParams>();
            var result = server.GetCodeActions(parameter);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.CodeActionResolveName)]
        public object GetResolvedCodeAction(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<CodeAction>();
            var result = server.GetResolvedCodeAction(parameter);
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }



        private static string Truncate(string text)
        {
            if (text.Length < MAX_LENGTH_DESCR_TEXT)
            {
                return text;
            }
            return text.Substring(0, MAX_LENGTH_DESCR_TEXT) + "...";
        }

        private IEnumerable<CompletionItem> Selected_Completions(bool useCapitals, ISet<AsmDude2.AsmTokenType> selectedTypes, bool addSpecialKeywords)
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
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<CompletionParams>();

            List<CompletionItem> items = new List<CompletionItem>();

            string line = this.server.GetLine(parameter.Position.Line, parameter.TextDocument);
            (string label, Mnemonic mnemonic, string[] args, string remark) = AsmSourceTools.ParseLine(line);

            if (mnemonic == Mnemonic.NONE) {
                ISet<AsmDude2.AsmTokenType> selected1 = new HashSet<AsmDude2.AsmTokenType> { AsmDude2.AsmTokenType.Directive, AsmDude2.AsmTokenType.Jump, AsmDude2.AsmTokenType.Misc, AsmDude2.AsmTokenType.Mnemonic };
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
                    if (ItemCommitCharacters)
                    {
                        item.CommitCharacters = ItemCommitCharacterArray;
                    }
                    else
                    {
                        item.CommitCharacters = null;
                    }

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
                IsIncomplete = this.IsIncomplete || parameter.Context.TriggerCharacter == "@" || (parameter.Context.TriggerKind == CompletionTriggerKind.TriggerForIncompleteCompletions && this.completionItemsNumberRoot % 50 != 0),
                Items = items.ToArray(),
            };

            this.server.IsIncomplete = false;

            if (this.CompletionServerError)
            {
                throw new InvalidOperationException("Simulated server error.");
            }

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(list)}");

            return list;
        }





        [JsonRpcMethod(Methods.TextDocumentSignatureHelpName)]
        public SignatureHelp OnTextDocumentSignatureHelp(JToken arg)
        {
            this.LogInfo($"Received: {arg}");

            string msg = "1:";
            TextDocumentPositionParams x = arg.ToObject<SignatureHelpParams>();
            if (x == null)
            {
                msg += $"x=null; arg={arg}";
            }
            else
            {
                var line = x.Position.Line;
                var character = x.Position.Character;
                msg += $"Position.Line {line}; Position.Character {character}; TextDocument {x.TextDocument}";
            }



            // 1 find the keyword
            // 2 find the parameter (that is, count the comma's)
            // 3 select from the list of signatures, the ones that are applicable


            SignatureHelp retVal = new SignatureHelp()
            {
                ActiveParameter = 1,
                ActiveSignature = 1,
                Signatures = new SignatureInformation[]
                {
                    new SignatureInformation()
                    {
                        Label = msg,
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
            };

            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(retVal)}");

            return retVal;
        }

        [JsonRpcMethod(Methods.WorkspaceDidChangeConfigurationName)]
        public void OnDidChangeConfiguration(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var parameter = arg.ToObject<DidChangeConfigurationParams>();
            this.server.SendSettings(parameter);
        }

        [JsonRpcMethod(Methods.ShutdownName)]
        public object Shutdown()
        {
            this.LogInfo($"Received Shutdown notification");
            return null;
        }

        [JsonRpcMethod(Methods.ExitName)]
        public void Exit()
        {
            this.LogInfo($"Received Exit notification");
            server.Exit();
        }

        [JsonRpcMethod(Methods.TextDocumentRenameName)]
        public WorkspaceEdit Rename(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
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

            this.LogInfo($"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentFoldingRangeName)]
        public object GetFoldingRanges(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            return this.server.GetFoldingRanges();
        }

        [JsonRpcMethod(VSMethods.GetProjectContextsName)]
        public object GetProjectContexts(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            var result = this.server.GetProjectContexts();
            this.traceSource.TraceEvent(TraceEventType.Information, 0, $"Sent: {JToken.FromObject(result)}");
            return result;
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentSymbolName)]
        public object GetDocumentSymbols(JToken arg)
        {
            this.LogInfo($"Received: {arg}");
            return this.server.GetDocumentSymbols();
        }

        [JsonRpcMethod(Methods.TextDocumentDocumentHighlightName, UseSingleObjectParameterDeserialization = true)]
        public DocumentHighlight[] GetDocumentHighlights(DocumentHighlightParams arg, CancellationToken token)
        {
            this.LogInfo($"Received: {JToken.FromObject(arg)}");
            var result = this.server.GetDocumentHighlights(arg.PartialResultToken, arg.Position, token);
            this.LogInfo($"Sent: {JToken.FromObject(result)}");
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


        #region logging copied from AsmDudeToolsStatic
        /*
        public static async Task<IVsOutputWindowPane> GetOutputPaneAsync()
        {
            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            IVsOutputWindow outputWindow = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
            {
                return null;
            }
            else
            {
                Guid paneGuid = new Guid("F97896F3-19AB-4E1F-A9C4-E11D489E5142");
                outputWindow.CreatePane(paneGuid, "AsmDude2", 1, 0);
                outputWindow.GetPane(paneGuid, out IVsOutputWindowPane pane);
                return pane;
            }
        }

        public static async System.Threading.Tasks.Task OutputAsync(string msg)
        {
            Contract.Requires(msg != null);

            if (!ThreadHelper.CheckAccess())
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);
            IVsOutputWindowPane outputPane = await GetOutputPaneAsync().ConfigureAwait(true);
            if (outputPane == null)
            {
                Debug.Write(msg2);
            }
            else
            {
                outputPane.OutputString(msg2);
                outputPane.Activate();
            }
        }
        */
        #endregion
    }
}
