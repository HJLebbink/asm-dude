using AsmDude.Tools;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AsmDude.QuickInfo
{
    /// <summary>
    /// Interaction logic for TooltipWindow2.xaml
    /// </summary>
    public partial class TooltipWindow2: UserControl
    {
        public TooltipWindow2()
        {
            InitializeComponent();
        }

        private void Calc_Btn_A_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("TooltipWindow2:Calc_Btn_A_Click");
        }

        private void Calc_Btn_B_Click(object sender, RoutedEventArgs e)
        {
            AsmDudeToolsStatic.Output_INFO("TooltipWindow2:Calc_Btn_B_Click");
        }
    }
}
