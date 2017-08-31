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
    public class ButtonInfo
    {
        public readonly Label text;
        public readonly Rn reg;
        public readonly bool before;

        public ButtonInfo(Label labelText, Rn reg, bool before)
        {
            this.text = labelText;
            this.reg = reg;
            this.before = before;
        }
    }


    /// <summary>
    /// Interaction logic for TooltipWindow2.xaml
    /// </summary>
    public partial class RegisterTooltipWindow: UserControl
    {
        private readonly AsmSimulator _asmSimulator;
        private readonly Rn _reg;
        private readonly int _lineNumber;

        public RegisterTooltipWindow(AsmSimulator asmSimulator, Rn reg, int lineNumber)
        {
            this._asmSimulator = asmSimulator;
            this._reg = reg;
            this._lineNumber = lineNumber;

            InitializeComponent();
            if (this._asmSimulator.Enabled & Settings.Default.AsmSim_Decorate_Registers)
            {
                this.Generate(true);
                this.Generate(false);
            }
            else
            {
                this.registerGrid.Visibility = Visibility.Collapsed;
            }
        }

        public void AddDescription(string reg, string description, Brush foreground)
        {
            this.description.Inlines.Add(Make_Bold_Run("Register ", foreground));
            this.description.Inlines.Add(Make_Bold_Run(reg, new SolidColorBrush(AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register))));

            if (description.Length > 0)
            {
                if (reg.Length > (AsmDudePackage.maxNumberOfCharsInToolTips / 2)) description = "\n" + description;
                this.description.Inlines.Add(new Run(AsmSourceTools.Linewrap(": " + description, AsmDudePackage.maxNumberOfCharsInToolTips))
                {
                    Foreground = foreground
                });
            }
        }

        private void Generate(bool isBefore)
        {
            int row = (isBefore) ? 0 : 1;
            {
                var label = new Label() { Content = (isBefore) ? "Before:" : "After:" };
                this.registerGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
            }
            {
                var label = new Label();
                this.registerGrid.Children.Add(label);
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 1);

                var register_Content = this._asmSimulator.Get_Register_Value_If_Already_Computed(this._reg, this._lineNumber, true);

                if (register_Content == null)
                {
                    label.Visibility = Visibility.Hidden;
                    var button = new Button()
                    {
                        Content = "Determine " + this._reg.ToString(),
                        Tag = new ButtonInfo(label, this._reg, true)
                    };
                    this.registerGrid.Children.Add(button);
                    Grid.SetRow(button, row);
                    Grid.SetColumn(button, 1);
                    button.Click += (sender, e) => { this.Update_Async(sender as Button); };
                }
                else
                {
                    label.Content = register_Content;
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
                        info.text.Content = this._asmSimulator.Get_Register_Value_and_Block(this._reg, this._lineNumber, info.before);
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
