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

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;

using AsmTools;
using AsmDude.Tools;

namespace AsmDude.QuickInfo
{
    public partial class RegisterTooltipWindow: UserControl
    {
        private readonly Brush _foreground;

        private int _lineNumber;
        private AsmSimulator _asmSimulator;

        public RegisterTooltipWindow(Brush foreground)
        {
            this._foreground = foreground;
            InitializeComponent();
        }

        public void SetDescription(Rn reg, AsmDudeTools asmDudeTools)
        {
            string regStr = reg.ToString();
            this.Description.Inlines.Add(Make_Bold_Run("Register ", this._foreground));
            this.Description.Inlines.Add(Make_Bold_Run(regStr, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register))));

            string description = asmDudeTools.Get_Description(regStr);
            if (description.Length > 0)
            {
                if (regStr.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) description = "\n" + description;
                this.Description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + description, AsmDudePackage.maxNumberOfCharsInToolTips))
                {
                    Foreground = this._foreground
                });
            }
        }

        public void SetAsmSim(AsmSimulator asmSimulator, Rn reg, int lineNumber, bool isExpanded)
        {
            this._asmSimulator = asmSimulator;
            this._lineNumber = lineNumber;

            bool empty = true;

            if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Show_Register_In_Register_Tooltip)
            {
                this.AsmSimGridExpander.IsExpanded = isExpanded;

                this.Generate(true, reg);
                this.Generate(false, reg);
                empty = false;
            }

            this.AsmSimGridExpander.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
            this.AsmSimGridBorder.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Generate(bool isBefore, Rn reg)
        {
            FontFamily f = new FontFamily("Consolas");

            int row = (isBefore) ? 1 : 2;
            {
                var label = new Label() {
                    Content = (isBefore) ? "Before:" : "After:",
                    FontFamily = f,
                    Foreground = this._foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Visible
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
            }
            {
                var label = new Label()
                {
                    FontFamily = f,
                    Foreground = this._foreground,
                    Height = 26
                    //VerticalAlignment = VerticalAlignment.Center,
                    //VerticalContentAlignment = VerticalAlignment.Center
                };
                this.AsmSimGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 1);

                var register_Content = this._asmSimulator.Get_Register_Value_If_Already_Computed(reg, this._lineNumber, isBefore, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration));

                if (register_Content == null)
                {
                    label.Visibility = Visibility.Collapsed;
                    var button = new Button()
                    {
                        Content = "Determine " + reg.ToString(),
                        Foreground = this._foreground,
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(label, reg, true)
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, 1);
                    button.Click += (sender, e) => { this.Update_Async(sender as Button); };
                }
                else
                {
                    label.Content = register_Content;
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
                            info.text.Content = this._asmSimulator.Get_Flag_Value_and_Block(info.flag, this._lineNumber, info.before);
                        }
                        else
                        {
                            info.text.Content = this._asmSimulator.Get_Register_Value_and_Block(info.reg, this._lineNumber, info.before, AsmSourceTools.ParseNumeration(Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration));
                        }
                        info.text.Visibility = Visibility.Visible;
                        button.Visibility = Visibility.Collapsed;
                    }));
                }
                catch (Exception e)
                {
                    AsmDudeToolsStatic.Output_ERROR("RegisterTooltipWindow: Update_Async: e=" + e.ToString());
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
