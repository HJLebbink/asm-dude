using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel;

namespace LanguageServerWithUI
{
    public class SymbolInformationItem : INotifyPropertyChanged
    {
        private string name;
        private SymbolKind kind;
        private string container;
        private string vsKind;
        private string vsKindModifier;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get => this.name;
            set
            {
                if (this.name != value)
                {
                    this.name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public SymbolKind Kind
        {
            get => this.kind;
            set
            {
                if (this.kind != value)
                {
                    this.kind = value;
                    OnPropertyChanged(nameof(Kind));
                }
            }
        }

        public string Container
        {
            get => this.container;
            set
            {
                if (this.container != value)
                {
                    this.container = value;
                    OnPropertyChanged(nameof(Container));
                }
            }
        }

        public string VsKind
        {
            get => this.vsKind;
            set
            {
                if (this.vsKind != value)
                {
                    this.vsKind = value;
                    OnPropertyChanged(nameof(VsKind));
                }
            }
        }

        public string VsKindModifier
        {
            get => this.vsKindModifier;
            set
            {
                if (this.vsKindModifier != value)
                {
                    this.vsKindModifier = value;
                    OnPropertyChanged(nameof(VsKindModifier));
                }
            }
        }

        private void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
