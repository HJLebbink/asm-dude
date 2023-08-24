using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel;

namespace LanguageServerWithUI
{
    public class DiagnosticItem : INotifyPropertyChanged
    {
        private string text;
        private ProjectContextItem context;
        private DiagnosticSeverity severity;
        private MockDiagnosticTags tag;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Text
        {
            get => this.text;
            set
            {
                if (this.text != value)
                {
                    this.text = value;
                    OnPropertyChanged(nameof(Text));
                }
            }
        }

        public ProjectContextItem Context
        {
            get => this.context;
            set
            {
                if (this.context != value)
                {
                    this.context = value;
                    OnPropertyChanged(nameof(Context));
                }
            }
        }

        public DiagnosticSeverity Severity
        {
            get => this.severity;
            set
            {
                if (this.severity != value)
                {
                    this.severity = value;
                    OnPropertyChanged(nameof(Severity));
                }
            }
        }

        public MockDiagnosticTags Tag
        {
            get => this.tag;
            set
            {
                if (this.tag != value)
                {
                    this.tag = value;
                    OnPropertyChanged(nameof(tag));
                }
            }
        }

        private void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
