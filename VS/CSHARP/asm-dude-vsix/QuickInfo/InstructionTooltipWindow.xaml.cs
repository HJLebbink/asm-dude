// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using AsmDude.Tools;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AsmTools;
using System.Windows.Media;
using System.Windows.Documents;

namespace AsmDude.QuickInfo
{

    public partial class InstructionTooltipWindow: UserControl
    {
        private readonly Brush _foreground;

        private int _lineNumber;
        private AsmSimulator _asmSimulator;

        public InstructionTooltipWindow(Brush foreground)
        {
            this._foreground = foreground;
            InitializeComponent();
        }

        public void SetDescription(Mnemonic mnemonic, AsmDudeTools asmDudeTools)
        {
            this.Description.Inlines.Add(Make_Bold_Run("Mnemonic ", this._foreground));
            this.Description.Inlines.Add(Make_Bold_Run(mnemonic.ToString(), new SolidColorBrush(AsmDudeToolsStatic.ConvertColor((AsmSourceTools.IsJump(mnemonic) ? Settings.Default.SyntaxHighlighting_Jump : Settings.Default.SyntaxHighlighting_Opcode)))));
            {
                string archStr = ":" + ArchTools.ToString(asmDudeTools.Mnemonic_Store.GetArch(mnemonic)) + " ";
                string descr = asmDudeTools.Mnemonic_Store.GetDescription(mnemonic);
                this.Description.Inlines.Add(new Run(AsmSourceTools.Linewrap(archStr + descr, AsmDudePackage.maxNumberOfCharsInToolTips))
                {
                    Foreground = this._foreground
                });
            }
        }

        public void SetPerformanceInfo(Mnemonic mnemonic, AsmDudeTools asmDudeTools, bool isExpanded)
        {
            this.PerformanceExpander.IsExpanded = isExpanded;

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
                    this.Performance.Inlines.Add(new Run(string.Format(format,
                        "", "", "µOps", "µOps", "µOps", "", "", ""))
                    {
                        FontFamily = family,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        Foreground = this._foreground
                    });
                    this.Performance.Inlines.Add(new Run(string.Format("\n" + format,
                        "Architecture", "Instruction", "Fused", "Unfused", "Port", "Latency", "Throughput", ""))
                    {
                        FontFamily = family,
                        FontStyle = FontStyles.Italic,
                        FontWeight = FontWeights.Bold,
                        Foreground = this._foreground
                    });
                }
                this.Performance.Inlines.Add(new Run(string.Format("\n" + format,
                    item._microArch,
                    item._instr + " " + item._args,
                    item._mu_Ops_Fused,
                    item._mu_Ops_Merged,
                    item._mu_Ops_Port,
                    item._latency,
                    item._throughput,
                    item._remark))
                {
                    FontFamily = family,
                    Foreground = this._foreground
                });
            }

            this.PerformanceExpander.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
            this.PerformanceBorder.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SetAsmSim(AsmSimulator asmSimulator, int lineNumber, bool isExpanded)
        {
            this._asmSimulator = asmSimulator;
            this._lineNumber = lineNumber;

            bool empty = true;

            if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip)
            {
                var usage = this._asmSimulator.Get_Usage(lineNumber);
                var readReg = new HashSet<Rn>(usage.ReadReg);
                var writeReg = new HashSet<Rn>(usage.WriteReg);

                this.GenerateHeader();
                int row = 2;

                foreach (Rn reg in Enum.GetValues(typeof(Rn)))
                {
                    bool b1 = readReg.Contains(reg);
                    bool b2 = writeReg.Contains(reg);
                    if (b1 || b2)
                    {
                        empty = false;
                        if (b1) this.Generate(reg, true, row);
                        if (b2) this.Generate(reg, false, row);
                        row++;
                    }
                }

                foreach (Flags flag in FlagTools.GetFlags(usage.ReadFlag | usage.WriteFlag))
                {
                    if (flag == Flags.NONE) continue;
                    bool b1 = usage.ReadFlag.HasFlag(flag);
                    bool b2 = usage.WriteFlag.HasFlag(flag);
                    if (b1 || b2)
                    {
                        empty = false;
                        if (b1) this.Generate(flag, true, row);
                        if (b2) this.Generate(flag, false, row);
                        row++;
                    }
                }
            }

            this.AsmSimGridExpander.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
            this.AsmSimGridExpander.IsExpanded = (empty) ? false : isExpanded;
            this.AsmSimGridBorder.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void GenerateHeader()
        {
            int row = 1;
            FontFamily f = new FontFamily("Consolas");
            {
                var label = new Label()
                {
                    Content= "Read:",
                    FontFamily = f,
                    Foreground = this._foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
            }
            {
                var label = new Label()
                {
                    Content = "Write:",
                    FontFamily = f,
                    Foreground = this._foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 1);
            }
        }

        private void Generate(Rn reg, bool isBefore, int row)
        {
            FontFamily f = new FontFamily("Consolas");

            int column = (isBefore) ? 0 : 1;
            {
                var label = new Label()
                {
                    FontFamily = f,
                    Foreground = this._foreground,
                    Height = Double.NaN
                    //VerticalAlignment = VerticalAlignment.Center,
                    //VerticalContentAlignment = VerticalAlignment.Center
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, column);

                string register_Content = this._asmSimulator.Get_Register_Value_If_Already_Computed(reg, this._lineNumber, isBefore, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration));

                if (register_Content == null)
                {
                    label.Visibility = Visibility.Collapsed;
                    var button = new Button()
                    {
                        Content = "Determine "+ reg.ToString(),
                        Foreground = this._foreground,
                        //VerticalAlignment = VerticalAlignment.Center,
                        //VerticalContentAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(label, reg, isBefore)
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, column);
                    button.Click += (sender, e) => { this.Update_Async(sender as Button); };
                }
                else
                {
                    label.Content = reg.ToString() + " = " + register_Content;
                    label.Visibility = Visibility.Visible;
                }
            }
        }

        private void Generate(Flags flag, bool isBefore, int row)
        {
            FontFamily f = new FontFamily("Consolas");

            int column = (isBefore) ? 0 : 1;
            {
                var label = new Label()
                {
                    FontFamily = f,
                    Foreground = this._foreground,
                    Height = Double.NaN
                    //VerticalAlignment = VerticalAlignment.Center,
                    //VerticalContentAlignment = VerticalAlignment.Center
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, column);

                string flag_Content = this._asmSimulator.Get_Flag_Value_If_Already_Computed(flag, this._lineNumber, isBefore);
                if (flag_Content == null)
                {
                    label.Visibility = Visibility.Collapsed;
                    var button = new Button()
                    {
                        Content = "Determine " + flag.ToString(),
                        Foreground = this._foreground,
                        //VerticalAlignment = VerticalAlignment.Center,
                        //VerticalContentAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(label, flag, isBefore)
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, column);
                    button.Click += (sender, e) => { this.Update_Async(sender as Button); };
                }
                else
                {
                    label.Content = flag.ToString() + " = " + flag_Content;
                    label.Visibility = Visibility.Visible;
                }
            }
        }

        private async void Update_Async(Button button)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (button == null) return;
                    if (this._asmSimulator == null) return;
                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        ButtonInfo info = (ButtonInfo)button.Tag;
                        if (info.reg == Rn.NOREG)
                        {
                            info.text.Content = info.flag.ToString() + " = " + this._asmSimulator.Get_Flag_Value_and_Block(info.flag, this._lineNumber, info.before);
                        }
                        else
                        {
                            info.text.Content = info.reg.ToString() + " = " + this._asmSimulator.Get_Register_Value_and_Block(info.reg, this._lineNumber, info.before, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip_Numeration));
                        }
                        info.text.Visibility = Visibility.Visible;
                        button.Visibility = Visibility.Collapsed;
                    }));
                }
                catch (Exception e)
                {
                    AsmDudeToolsStatic.Output_ERROR("InstructionTooltipWindow: Update_Async: e=" + e.ToString());
                }
            });
        }

        private static Run Make_Bold_Run(string str, Brush foreground)
        {
            return new Run(str)
            {
                FontWeight = FontWeights.Bold,
                Foreground = foreground
            };
        }
    }
}
