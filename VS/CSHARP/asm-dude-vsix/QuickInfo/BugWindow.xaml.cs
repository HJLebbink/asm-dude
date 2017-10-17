using System.Windows;
using System.Windows.Controls;

using AsmDude.Tools;

namespace AsmDude.QuickInfo
{
    public partial class BugWindow : UserControl
    {
        public BugWindow()
        {
            InitializeComponent();

            this.MainWindow.MouseLeftButtonDown += (o, i) => {
                AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:MouseLeftButtonDown Event");
                i.Handled = true; // dont let the mouse event from inside this window bubble up to VS
            }; 

            this.MainWindow.PreviewMouseLeftButtonDown += (o, i) =>
            {
                //i.Handled = true; // if true then no event is able to bubble to the gui
                AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PreviewMouseLeftButtonDown Event");
            };
        }

        private void GotMouseCapture_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("InstructionTooltipWindow:PerformanceExpander_Click");
            e.Handled = true;
        }
    }
}
