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

    /// <summary>
    /// Interaction logic for TooltipWindow2.xaml
    /// </summary>
    public partial class InstructionTooltipWindow: UserControl
    {
        private readonly AsmSimulator _asmSimulator;
        private readonly int _lineNumber;

        public InstructionTooltipWindow(AsmSimulator asmSimulator, int lineNumber)
        {
            this._asmSimulator = asmSimulator;
            this._lineNumber = lineNumber;

            InitializeComponent();
            if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Show_Register_In_Instruction_Tooltip)
            {
                this.Generate(true);
                this.Generate(false);
            }
        }

        public void AddDescription(Mnemonic mnemonic, string description, Brush foreground)
        {
            string mnemonicStr = mnemonic.ToString();
            this.description.Inlines.Add(Make_Bold_Run("Mnemonic ", foreground));
            this.description.Inlines.Add(Make_Bold_Run(mnemonicStr, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register))));

            if (description.Length > 0)
            {
                if (mnemonicStr.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) description = "\n" + description;
                this.description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + description, AsmDudePackage.maxNumberOfCharsInToolTips))
                {
                    Foreground = foreground
                });
            }
        }

        private void Generate(bool isBefore)
        {
            FontFamily f = new FontFamily("Consolas");
            //FontFamily f = new FontFamily("Consolas");

            int row = (isBefore) ? 1 : 2;
            {
                var label = new Label() {
                    Content = (isBefore) ? "Before:" : "After:",
                    FontFamily = f,
                    //Height = height,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Visible
                };
                this.ContentGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
            }
            {
                var label = new Label()
                {
                    FontFamily = f,
                    VerticalAlignment = VerticalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                this.ContentGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 1);

                string register_Content = null;// this._asmSimulator.Get_Register_Value_If_Already_Computed(this._reg, this._lineNumber, isBefore);

                if (register_Content == null)
                {
                    label.Visibility = Visibility.Collapsed;
                    var button = new Button()
                    {
                        Content = "Determine ",// + this._reg.ToString(),
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Visible,
                        Tag = new ButtonInfo(label,Rn.RAX, true)
                    };
                    this.ContentGrid.Children.Add(button);
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
                    this.Dispatcher.Invoke((Action)(() =>
                    {
                        ButtonInfo info = (ButtonInfo)button.Tag;
                        info.text.Content = this._asmSimulator.Get_Register_Value_and_Block(Rn.RAX, this._lineNumber, info.before);
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
