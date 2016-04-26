using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude.SyntaxHighlighting {

    internal static class AsmClassificationDefinition {

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("mnemonic")]
        internal static ClassificationTypeDefinition mnemonic = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("register")]
        internal static ClassificationTypeDefinition register = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("remark")]
        internal static ClassificationTypeDefinition remark = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("directive")]
        internal static ClassificationTypeDefinition directive = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("constant")]
        internal static ClassificationTypeDefinition constant = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("jump")]
        internal static ClassificationTypeDefinition jump = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("label")]
        internal static ClassificationTypeDefinition label = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("misc")]
        internal static ClassificationTypeDefinition misc = null;
    }
}
