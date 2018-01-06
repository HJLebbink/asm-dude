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

        private TextBox _textBox_before;
        private TextBox _textBox_after;

        private int _lineNumber;
        private AsmSimulator _asmSimulator;

        public RegisterTooltipWindow(Brush foreground)
        {
            this._foreground = foreground;
            InitializeComponent();

            this.AsmSimGridExpander.Collapsed += (o, i) => { this.AsmSimGridExpanderNumeration.Visibility = Visibility.Collapsed; };
            this.AsmSimGridExpander.Expanded += (o, i) => { this.AsmSimGridExpanderNumeration.Visibility = Visibility.Visible; };
        }

        public void SetDescription(Rn reg, AsmDudeTools asmDudeTools)
        {
            string regStr = reg.ToString();

            this.Description.Inlines.Add(new Run("Register ") { FontWeight = FontWeights.Bold, Foreground = this._foreground });
            this.Description.Inlines.Add(new Run(regStr) { FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register))});

            Arch arch = RegisterTools.GetArch(reg);
            string archStr = (arch == Arch.NONE) ? "" : " [" + ArchTools.ToString(arch) + "] ";
            string descr = asmDudeTools.Get_Description(regStr);

            if (regStr.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) descr = "\n" + descr;
            string full_Descr = AsmSourceTools.Linewrap(":" + archStr + descr, AsmDudePackage.maxNumberOfCharsInToolTips);
            this.Description.Inlines.Add(new Run(full_Descr) { Foreground = this._foreground });
        }

        public void SetAsmSim(AsmSimulator asmSimulator, Rn reg, int lineNumber, bool isExpanded)
        {
            this._asmSimulator = asmSimulator;
            this._lineNumber = lineNumber;

            bool empty = true;

            if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Show_Register_In_Register_Tooltip)
            {
                this.AsmSimGridExpander.IsExpanded = isExpanded;
                this.AsmSimGridExpanderNumeration.Text = Settings.Default.AsmSim_Show_Register_In_Register_Tooltip_Numeration;

                this.Generate(true, reg);
                this.Generate(false, reg);
                empty = false;
            }

            this.AsmSimGridExpander.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;
            this.AsmSimGridBorder.Visibility = (empty) ? Visibility.Collapsed : Visibility.Visible;

            this.AsmSimGridExpanderNumeration.SelectionChanged += (sender, i) =>
            {
                string numerationStr = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
                NumerationEnum numeration = AsmSourceTools.ParseNumeration(numerationStr);
                if (numeration == NumerationEnum.UNKNOWN) AsmDudeToolsStatic.Output_WARNING("SetAsmSim:smSimGridExpanderNumeration.SelectionChanged: unknown numerationStr="+ numerationStr);
                //AsmDudeToolsStatic.Output_INFO("AsmSimGridExpanderNumeration:SelectionChanged: numeration="+ numeration);

                var content_before = this._asmSimulator.Get_Register_Value_If_Already_Computed(reg, this._lineNumber, true, numeration);
                if (content_before != null) this._textBox_before.Text = content_before;

                var content_after = this._asmSimulator.Get_Register_Value_If_Already_Computed(reg, this._lineNumber, false, numeration);
                if (content_after != null) this._textBox_after.Text = content_after;
            };
        }

        private void Generate(bool isBefore, Rn reg)
        {
            FontFamily f = new FontFamily("Consolas");

            int row = (isBefore) ? 1 : 2;
            {
                var textBlock = new TextBlock() {
                    Text = (isBefore) ? "Before:" : "After:",
                    FontFamily = f,
                    Foreground = this._foreground
                };
                this.AsmSimGrid.Children.Add(textBlock);
                Grid.SetRow(textBlock, row);
                Grid.SetColumn(textBlock, 0);
            }

            {
                var textBox = new TextBox()
                {
                    FontFamily = f,
                    Foreground = this._foreground,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap
                };

                if (isBefore)
                    this._textBox_before = textBox;
                else
                    this._textBox_after = textBox;

                this.AsmSimGrid.Children.Add(textBox);
                Grid.SetRow(textBox, row);
                Grid.SetColumn(textBox, 1);

                var register_Content = this._asmSimulator.Get_Register_Value_If_Already_Computed(reg, this._lineNumber, isBefore, AsmSourceTools.ParseNumeration(this.AsmSimGridExpanderNumeration.Text));
                if (register_Content == null)
                {
                    textBox.Visibility = Visibility.Collapsed;
                    var button = new Button()
                    {
                        Content = "Determine " + reg.ToString(),
                        Foreground = this._foreground,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(textBox, reg, isBefore)
                    };
                    this.AsmSimGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, 1);
                    button.Click += (sender, e) => { this.Update_Async(sender as Button); };
                }
                else
                {
                    textBox.Text = register_Content;
                    textBox.Visibility = Visibility.Visible;
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

                        info.text.Text = (info.reg == Rn.NOREG)
                            ? this._asmSimulator.Get_Flag_Value_and_Block(info.flag, this._lineNumber, info.before)
                            : this._asmSimulator.Get_Register_Value_and_Block(info.reg, this._lineNumber, info.before, AsmSourceTools.ParseNumeration(this.AsmSimGridExpanderNumeration.Text));

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
    }
}
