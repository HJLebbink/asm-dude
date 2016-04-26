using Microsoft.VisualStudio.Text.Tagging;

namespace AsmDude.SyntaxHighlighting {

    public class AsmTokenTag : ITag {
        public AsmTokenType type { get; private set; }

        public AsmTokenTag(AsmTokenType type) {
            this.type = type;
        }
    }
}
