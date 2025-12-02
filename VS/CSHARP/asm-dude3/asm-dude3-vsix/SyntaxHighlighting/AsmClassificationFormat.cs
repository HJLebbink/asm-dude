using System.ComponentModel.Composition;
using System.Windows.Media;
using AsmDude3.SyntaxHighlighting;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude3;

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Mnemonic)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class MnemonicFormat : ClassificationFormatDefinition
{
    public MnemonicFormat()
    {
        DisplayName = "AsmDude3 - Mnemonic";
        ForegroundColor = Color.FromRgb(86, 156, 214); // Blue
    }
}

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Register)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Register)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class RegisterFormat : ClassificationFormatDefinition
{
    public RegisterFormat()
    {
        DisplayName = "AsmDude3 - Register";
        ForegroundColor = Color.FromRgb(78, 201, 176); // Teal
    }
}

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Remark)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Remark)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class RemarkFormat : ClassificationFormatDefinition
{
    public RemarkFormat()
    {
        DisplayName = "AsmDude3 - Remark";
        ForegroundColor = Color.FromRgb(87, 166, 74); // Green
    }
}

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Constant)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Constant)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class ConstantFormat : ClassificationFormatDefinition
{
    public ConstantFormat()
    {
        DisplayName = "AsmDude3 - Constant";
        ForegroundColor = Color.FromRgb(181, 206, 168); // Light Green
    }
}

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Label)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Label)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class LabelFormat : ClassificationFormatDefinition
{
    public LabelFormat()
    {
        DisplayName = "AsmDude3 - Label";
        ForegroundColor = Color.FromRgb(220, 220, 170); // Yellow
    }
}

[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Misc)]
[Name(AsmClassificationDefinition.ClassificationTypeNames.Misc)]
[UserVisible(true)]
[Order(After = Priority.High)]
internal sealed class MiscFormat : ClassificationFormatDefinition
{
    public MiscFormat()
    {
        DisplayName = "AsmDude3 - Misc";
        ForegroundColor = Colors.White;
    }
}
