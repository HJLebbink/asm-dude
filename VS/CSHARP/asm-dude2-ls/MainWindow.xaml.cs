using System.Windows;

namespace AsmDude2LS
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel viewModel = new MainWindowViewModel();
        public MainWindow()
        {
            InitializeComponent();
            this.Hide();
            DataContext = viewModel;
            this.ShowInTaskbar = false;
        }
    }
}
