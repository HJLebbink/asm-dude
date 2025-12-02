using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude3.SyntaxHighlighting;

internal static class AsmClassificationDefinition
{
    internal static class ClassificationTypeNames
    {
        public const string Mnemonic = "asmdude3.mnemonic";
        public const string Register = "asmdude3.register";
        public const string Remark = "asmdude3.remark";
        public const string Constant = "asmdude3.constant";
        public const string Label = "asmdude3.label";
        public const string Misc = "asmdude3.misc";
    }

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Mnemonic)]
    internal static ClassificationTypeDefinition? Mnemonic = null;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Register)]
    internal static ClassificationTypeDefinition? Register = null;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Remark)]
    internal static ClassificationTypeDefinition? Remark = null;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Constant)]
    internal static ClassificationTypeDefinition? Constant = null;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Label)]
    internal static ClassificationTypeDefinition? Label = null;

    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ClassificationTypeNames.Misc)]
    internal static ClassificationTypeDefinition? Misc = null;
}
