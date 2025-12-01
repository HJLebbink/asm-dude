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

namespace AsmDude.QuickInfo
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Media;
    using AsmDude.Tools;
    using AsmTools;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Shell;

    public partial class InstructionTooltipWindow : IInteractiveQuickInfoContent
    {
        private readonly Brush foreground_;
        private IList<TextBox> itemsOnPage_;
        private int lineNumber_;
        private AsmSimulator asmSimulator_;

        internal AsmQuickInfoController Owner { get; set; }

        internal IAsyncQuickInfoSession Session { get; set; }

        public InstructionTooltipWindow(Brush foreground)
        {
            this.foreground_ = foreground;
            this.InitializeComponent();

            this.AsmSimGridExpander.Collapsed += (o, i) => { this.AsmSimGridExpanderNumeration.Visibility = Visibility.Collapsed; };
            this.AsmSimGridExpander.Expanded += (o, i) => { this.AsmSimGridExpanderNumeration.Visibility = Visibility.Visible; };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:CloseButton_Click");
            this.Owner?.CloseToolTip();
            this.Session?.DismissAsync();
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:CloseButton_Click: owner and session are null");
        }

        private void StackPanel_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:StackPanel_Click");
        }

        private void AsmSimExpander_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:AsmSimExpander_Click");
        }

        private void PerformanceExpander_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceExpander_Click");
        }

        private void PerformanceBorder_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceBorder_Click");
        }

        private void ScrollViewer_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:ScrollViewer_Click");
        }

        private void TextBlock_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:TextBlock_Click");
        }

        private void PerformanceExpander_MouseLeftDown(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceExpander_MouseLeftDown");
        }

        public void SetDescription(Mnemonic mnemonic, AsmDudeTools asmDudeTools)
        {
            Contract.Requires(asmDudeTools != null);
            string mnemonicStr = mnemonic.ToString();

            this.Description.Inlines.Add(new Run("Mnemonic ") { FontWeight = FontWeights.Bold, Foreground = this.foreground_ });
            this.Description.Inlines.Add(new Run(mnemonicStr) { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(AsmSourceTools.IsJump(mnemonic) ? Settings.Default.SyntaxHighlighting_Jump : Settings.Default.SyntaxHighlighting_Opcode)) });

            string archStr = ":" + ArchTools.ToString(asmDudeTools.Mnemonic_Store.GetArch(mnemonic)) + " ";
            string descr = asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
            string full_Descr = AsmSourceTools.Linewrap(archStr + descr, AsmDudePackage.MaxNumberOfCharsInToolTips);
            this.Description.Inlines.Add(new Run(full_Descr) { Foreground = this.foreground_ });
        }

        public void SetPerformanceInfo(Mnemonic mnemonic, AsmDudeTools asmDudeTools)
        {
            Contract.Requires(asmDudeTools != null);

            if (Settings.Default.PerformanceInfo_On)
            {
                this.PerformanceExpander.IsExpanded = !Settings.Default.PerformanceInfo_IsDefaultCollapsed;

                bool empty = true;
                bool first = true;
                FontFamily family = new FontFamily("Consolas");
                string format = "{0,-14}{1,-24}{2,-7}{3,-9}{4,-20}{5,-9}{6,-11}{7,-10}";

                MicroArch selectedMicroarchitures = AsmDudeToolsStatic.Get_MicroArch_Switched_On();
                foreach (PerformanceItem item in asmDudeTools.Performance_Store.GetPerformance(mnemonic, selectedMicroarchitures))
                {
                    empty = false;
                    if (first)
                    {
                        first = false;
                        this.Performance.Inlines.Add(new Run(string.Format(
                            AsmDudeToolsStatic.CultureUI,
                            format,
                            string.Empty, string.Empty, "µOps", "µOps", "µOps", string.Empty, string.Empty, string.Empty))
                        {
                            FontFamily = family,
                            FontStyle = FontStyles.Italic,
                            FontWeight = FontWeights.Bold,
                            Foreground = this.foreground_,
                        });
                        this.Performance.Inlines.Add(new Run(string.Format(
                            AsmDudeToolsStatic.CultureUI,
                            "\n" + format,
                            "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", string.Empty))
                        {
                            FontFamily = family,
                            FontStyle = FontStyles.Italic,
                            FontWeight = FontWeights.Bold,
                            Foreground = this.foreground_,
                        });
                    }
                    this.Performance.Inlines.Add(new Run(string.Format(
                        AsmDudeToolsStatic.CultureUI,
                        "\n" + format,
                        item.microArch_ + " ",
                        item.instr_ + " " + item.args_ + " ",
                        item.mu_Ops_Fused_ + " ",
                        item.mu_Ops_Merged_ + " ",
                        item.mu_Ops_Port_ + " ",
                        item.latency_ + " ",
                        item.throughput_ + " ",
                        item.remark_))
                    {
                        FontFamily = family,
                        Foreground = this.foreground_,
                    });
                }
                this.PerformanceExpander.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
                this.PerformanceBorder.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public void SetAsmSim(AsmSimulator asmSimulator, int lineNumber, bool isExpanded)
        {
            Contract.Requires(asmSimulator != null);

            this.asmSimulator_ = asmSimulator;
            this.lineNumber_ = lineNumber;

            bool empty = true;

            if (this.asmSimulator_.Enabled & Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip)
            {
                this.itemsOnPage_ = new List<TextBox>();

                this.AsmSimGridExpander.IsExpanded = isExpanded;
                this.AsmSimGridExpanderNumeration.Text = Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration;

                (IEnumerable<Rn> readReg1, IEnumerable<Rn> writeReg1, Flags readFlag, Flags writeFlag, bool memRead, bool memWrite) = this.asmSimulator_.Get_Usage(lineNumber);
                HashSet<Rn> readReg = new HashSet<Rn>(readReg1);
                HashSet<Rn> writeReg = new HashSet<Rn>(writeReg1);

                this.GenerateHeader();
                int row = 2;

                foreach (Rn reg in Enum.GetValues(typeof(Rn)))
                {
                    bool b1 = readReg.Contains(reg);
                    bool b2 = writeReg.Contains(reg);
                    if (b1 || b2)
                    {
                        empty = false;
                        if (b1)
                        {
                            this.Generate(reg, true, row);
                        }

                        if (b2)
                        {
                            this.Generate(reg, false, row);
                        }

                        row++;
                    }
                }

                foreach (Flags flag in FlagTools.GetFlags(readFlag | writeFlag))
                {
                    if (flag == Flags.NONE)
                    {
                        continue;
                    }

                    bool b1 = readFlag.HasFlag(flag);
                    bool b2 = writeFlag.HasFlag(flag);
                    if (b1 || b2)
                    {
                        empty = false;
                        if (b1)
                        {
                            this.Generate(flag, true, row);
                        }

                        if (b2)
                        {
                            this.Generate(flag, false, row);
                        }

                        row++;
                    }
                }
            }

            this.AsmSimGridExpander.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            this.AsmSimGridBorder.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

            this.AsmSimGridExpanderNumeration.SelectionChanged += (sender, i) =>
            {
                string numerationStr = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
                NumerationEnum numeration = AsmSourceTools.ParseNumeration(numerationStr, false);
                if (numeration == NumerationEnum.UNKNOWN)
                {
                    AsmDudeToolsStatic.Output_WARNING("SetAsmSim:smSimGridExpanderNumeration.SelectionChanged: unknown numerationStr=" + numerationStr);
                }
                //AsmDudeToolsStatic.Output_INFO("AsmSimGridExpanderNumeration:SelectionChanged: numeration="+ numeration);

                foreach (TextBox textBox in this.itemsOnPage_)
                {
                    ButtonInfo info = textBox.Tag as ButtonInfo;

                    string content = (info.Reg == Rn.NOREG)
                        ? this.asmSimulator_.Get_Flag_Value_If_Already_Computed(info.Flag, this.lineNumber_, info.Before)
                        : this.asmSimulator_.Get_Register_Value_If_Already_Computed(info.Reg, this.lineNumber_, info.Before, numeration);

                    if (content != null)
                    {
                        textBox.Text = info.Reg.ToString() + " = " + content;
                    }
                }
            };
        }

        public bool KeepQuickInfoOpen
        {
            get
            {
                //AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:KeepQuickInfoOpen");
                return this.IsMouseOverAggregated || this.IsKeyboardFocusWithin || this.IsKeyboardFocused || this.IsFocused;
            }
        }

        public bool IsMouseOverAggregated
        {
            get
            {
                //AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:IsMouseOverAggregated");
                return this.IsMouseOver || this.IsMouseDirectlyOver;
            }
        }

        private void GenerateHeader()
        {
            int row = 1;
            FontFamily f = new FontFamily("Consolas");
            {
                TextBlock textBlock = new TextBlock()
                {
                    Text = "Read: ",
                    FontFamily = f,
                    FontStyle = FontStyles.Italic,
                    FontWeight = FontWeights.Bold,
                    Foreground = this.foreground_,
                };
                this.AsmSimGrid.Children.Add(textBlock);
                Grid.SetRow(textBlock, row);
                Grid.SetColumn(textBlock, 0);
            }
            {
                TextBlock textBlock = new TextBlock()
                {
                    Text = "Write: ",
                    FontFamily = f,
                    FontStyle = FontStyles.Italic,
                    FontWeight = FontWeights.Bold,
                    Foreground = this.foreground_,
                };
                this.AsmSimGrid.Children.Add(textBlock);
                Grid.SetRow(textBlock, row);
                Grid.SetColumn(textBlock, 1);
            }
        }

        private void Generate(Rn reg, bool isBefore, int row)
        {
            FontFamily f = new FontFamily("Consolas");

            int column = isBefore ? 0 : 1;
            {
                TextBox textBox = new TextBox()
                {
                    FontFamily = f,
                    Foreground = this.foreground_,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Tag = new ButtonInfo(null, reg, isBefore),
                };
                this.itemsOnPage_.Add(textBox);
                this.AsmSimGrid.Children.Add(textBox);
                Grid.SetRow(textBox, row);
                Grid.SetColumn(textBox, column);

                string register_Content = this.asmSimulator_.Get_Register_Value_If_Already_Computed(reg, this.lineNumber_, isBefore, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration, false));
                if (register_Content == null)
                {
                    textBox.Visibility = Visibility.Collapsed;
                    Button button = new Button()
                    {
                        Content = "Determine " + reg.ToString(),
                        Foreground = this.foreground_,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(textBox, reg, isBefore),
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, column);
                    //TODO: is the following Event handler ever unsubscribed?
                    button.Click += (sender, e) => this.Update_Async(sender as Button).ConfigureAwait(false);
                }
                else
                {
                    textBox.Text = reg.ToString() + " = " + register_Content;
                    textBox.Visibility = Visibility.Visible;
                }
            }
        }

        private void Generate(Flags flag, bool isBefore, int row)
        {
            FontFamily f = new FontFamily("Consolas");

            int column = isBefore ? 0 : 1;
            {
                TextBox textBlock = new TextBox()
                {
                    FontFamily = f,
                    Foreground = this.foreground_,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                };
                this.AsmSimGrid.Children.Add(textBlock);
                Grid.SetRow(textBlock, row);
                Grid.SetColumn(textBlock, column);

                string flag_Content = this.asmSimulator_.Get_Flag_Value_If_Already_Computed(flag, this.lineNumber_, isBefore);
                if (flag_Content == null)
                {
                    textBlock.Visibility = Visibility.Collapsed;
                    Button button = new Button()
                    {
                        Content = "Determine " + flag.ToString(),
                        Foreground = this.foreground_,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(textBlock, flag, isBefore),
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, column);

                    button.GotFocus += (s, e) => AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:GotFocus", this.ToString()));
                    button.Click += (sender, e) =>
                    {
                        AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Click", this.ToString()));
                        this.Update_Async(sender as Button).ConfigureAwait(false);
                    };
                }
                else
                {
                    textBlock.Text = flag.ToString() + " = " + flag_Content;
                    textBlock.Visibility = Visibility.Visible;
                }
            }
        }

        private async System.Threading.Tasks.Task Update_Async(Button button)
        {
            AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Update_Async", this.ToString()));

            if (button == null)
            {
                return;
            }

            if (this.asmSimulator_ == null)
            {
                return;
            }

            try
            {
                if (!ThreadHelper.CheckAccess())
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                }

                ButtonInfo info = (ButtonInfo)button.Tag;
                info.Text.Text = (info.Reg == Rn.NOREG)
                    ? info.Flag.ToString() + " = " + this.asmSimulator_.Get_Flag_Value_and_Block(info.Flag, this.lineNumber_, info.Before)
                    : info.Reg.ToString() + " = " + this.asmSimulator_.Get_Register_Value_and_Block(info.Reg, this.lineNumber_, info.Before, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration, false));

                info.Text.Visibility = Visibility.Visible;
                button.Visibility = Visibility.Collapsed;
            }
            catch (Exception e)
            {
                AsmDudeToolsStatic.Output_ERROR(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Update_Async; e={1}", this.ToString(), e.ToString()));
            }
        }
    }
}
