using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude3.SyntaxHighlighting;

[Export(typeof(ITaggerProvider))]
[ContentType(AsmDude3Package.AsmDudeContentType)]
[TagType(typeof(ClassificationTag))]
[Name("AsmDude3 Classification Tagger Provider")]
internal sealed class AsmClassificationTaggerProvider : ITaggerProvider
{
    [Import]
    internal IClassificationTypeRegistryService ClassificationTypeRegistry { get; set; } = null!;

    public ITagger<T>? CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        if (buffer == null)
        {
            return null;
        }

        return buffer.Properties.GetOrCreateSingletonProperty(() =>
            new AsmClassificationTagger(buffer, ClassificationTypeRegistry)) as ITagger<T>;
    }
}
