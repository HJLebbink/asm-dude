using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.ComponentModel;

namespace LanguageServerWithUI
{
    public class ProjectContextItem : INotifyPropertyChanged
    {
        private string label;
        private VSProjectKind kind;
        private string id;
        private string vsKind;
        private string vsKindModifier;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Label
        {
            get => this.label;
            set
            {
                if (this.label != value)
                {
                    this.label = value;
                    OnPropertyChanged(nameof(Label));
                }
            }
        }

        public VSProjectKind Kind
        {
            get => this.kind;
            set
            {
                if (this.kind != value) {

                    }
                this.kind = value;
                OnPropertyChanged(nameof(Kind));
            }
        }

        public string Id
        {
            get => this.id;
            set
            {
                if (this.id != value) 
                {
                    this.id = value;
                    OnPropertyChanged(nameof(Id));
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

        public VSProjectContext ToVSContext()
        {
            var context = new VSProjectContext()
            {
                Label = this.Label,
                Kind = this.Kind,
                Id = this.Id,
            };

            return context;
        }
    }
}
