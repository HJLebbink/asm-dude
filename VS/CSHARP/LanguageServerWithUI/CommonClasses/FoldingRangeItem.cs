using System.ComponentModel;

namespace LanguageServerWithUI
{
    public class FoldingRangeItem : INotifyPropertyChanged
    {
        private int startLine;
        private int? startCharacter;
        private int endLine;
        private int? endCharacter;

        public event PropertyChangedEventHandler PropertyChanged;

        public int StartLine
        {
            get => this.startLine;
            set
            {
                if (this.startLine != value)
                {
                    this.startLine = value;
                    OnPropertyChanged(nameof(StartLine));
                }
            }
        }

        public int? StartCharacter
        {
            get => this.startCharacter;
            set
            {
                if (this.startCharacter != value)
                {
                    this.startCharacter = value;
                    OnPropertyChanged(nameof(StartCharacter));
                }
            }
        }

        public int EndLine
        {
            get => this.endLine;
            set
            {
                if (this.endLine != value)
                {
                    this.endLine = value;
                    OnPropertyChanged(nameof(EndLine));
                }
            }
        }

        public int? EndCharacter
        {
            get => this.endCharacter;
            set
            {
                if (this.endCharacter != value)
                {
                    this.endCharacter = value;
                    OnPropertyChanged(nameof(EndCharacter));
                }
            }
        }

        private void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
