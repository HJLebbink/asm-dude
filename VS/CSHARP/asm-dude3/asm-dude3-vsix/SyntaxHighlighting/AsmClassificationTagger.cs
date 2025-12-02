using System;
using System.Collections.Generic;
using AsmTools;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace AsmDude3.SyntaxHighlighting;

internal sealed class AsmClassificationTagger : ITagger<ClassificationTag>
{
    private readonly ITextBuffer _buffer;
    private readonly AsmDude2Tools _asmDudeTools;
    private readonly ClassificationTag _mnemonicTag;
    private readonly ClassificationTag _registerTag;
    private readonly ClassificationTag _remarkTag;
    private readonly ClassificationTag _constantTag;
    private readonly ClassificationTag _labelTag;
    private readonly ClassificationTag _miscTag;

    public AsmClassificationTagger(ITextBuffer buffer, IClassificationTypeRegistryService typeService)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        // Initialize AsmDude2Tools with installation path
        string installPath = GetInstallPath();
        _asmDudeTools = AsmDude2Tools.Create(installPath, null);

        _mnemonicTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic));
        _registerTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Register));
        _remarkTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Remark));
        _constantTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Constant));
        _labelTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Label));
        _miscTag = new ClassificationTag(typeService.GetClassificationType(AsmClassificationDefinition.ClassificationTypeNames.Misc));
    }

    private string GetInstallPath()
    {
        // Get the installation path from the VSIX install directory
        try
        {
            string? codeBase = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(codeBase))
            {
                string? directory = System.IO.Path.GetDirectoryName(codeBase);
                if (!string.IsNullOrEmpty(directory))
                {
                    return directory;
                }
            }
        }
        catch
        {
            // Fallback to empty string if we can't get the path
        }
        return string.Empty;
    }

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged
    {
        add { }
        remove { }
    }

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0)
        {
            yield break;
        }

        foreach (SnapshotSpan curSpan in spans)
        {
            string content = curSpan.GetText();
            string[] lines = content.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length > 0)
            {
                int startLineNumber = curSpan.Start.GetContainingLineNumber();
                var snapshot = curSpan.Snapshot;
                int offset = curSpan.Start.GetContainingLine().Start.Position;

                for (int i = 0; i < lines.Length; ++i)
                {
                    string lineStr = lines[i];
                    if (lineStr.Length > 0)
                    {
                        // Use Parse.ParseMasm from asm-tools-lib to tokenize the line
                        foreach ((int beginPos, int endPos, AsmTokenType type) in Parse.ParseMasm(lineStr, _asmDudeTools))
                        {
                            int length = endPos - beginPos;
                            int beginPosOverall = beginPos + offset;
                            yield return new TagSpan<ClassificationTag>(
                                new SnapshotSpan(snapshot, new Span(beginPosOverall, length)),
                                GetClassificationTag(type));
                        }
                    }
                    offset += lineStr.Length + Environment.NewLine.Length;
                }
            }
        }
    }

    private ClassificationTag GetClassificationTag(AsmTokenType type)
    {
        return type switch
        {
            AsmTokenType.Mnemonic => _mnemonicTag,
            AsmTokenType.Register => _registerTag,
            AsmTokenType.Remark => _remarkTag,
            AsmTokenType.Constant => _constantTag,
            AsmTokenType.Label => _labelTag,
            AsmTokenType.LabelDef => _labelTag,
            _ => _miscTag
        };
    }
}
