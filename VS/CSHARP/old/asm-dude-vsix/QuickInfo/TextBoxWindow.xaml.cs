namespace AsmDude.QuickInfo
{
    using Microsoft.VisualStudio.Language.Intellisense;

    /// <summary>
    /// Interaction logic for TextBoxWindow.xaml
    /// </summary>
    public partial class TextBoxWindow : IInteractiveQuickInfoContent
    {
        public TextBoxWindow()
        {
            this.InitializeComponent();
        }

        public bool KeepQuickInfoOpen
        {
            get
            {
                return this.IsMouseOverAggregated || this.IsKeyboardFocusWithin || this.IsKeyboardFocused || this.IsFocused;
            }
        }

        public bool IsMouseOverAggregated
        {
            get
            {
                return this.IsMouseOver || this.IsMouseDirectlyOver;
            }
        }
    }
}
