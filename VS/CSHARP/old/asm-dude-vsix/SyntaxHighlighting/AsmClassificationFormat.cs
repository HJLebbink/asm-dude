// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude
{
    using System.ComponentModel.Composition;
    using AsmDude.SyntaxHighlighting;
    using AsmDude.Tools;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(EditorFormatDefinition))] // export as EditorFormatDefinition otherwise the syntax coloring does not work
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Mnemonic)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Mnemonic)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class OpcodeP : ClassificationFormatDefinition
    {
        public OpcodeP()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            this.DisplayName = "AsmDude - Syntax Highlighting - Mnemonic"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Opcode);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Opcode_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))] // export as EditorFormatDefinition otherwise the syntax coloring does not work
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.MnemonicOff)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.MnemonicOff)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class OpcodeOffP : ClassificationFormatDefinition
    {
        public OpcodeOffP()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            this.DisplayName = "AsmDude - Syntax Highlighting - Mnemonic (switched off)"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Opcode);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Opcode_Italic;
            this.ForegroundOpacity = 0.4;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Register)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Register)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class RegisterP : ClassificationFormatDefinition
    {
        public RegisterP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Register"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Register_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Remark)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Remark)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class RemarkP : ClassificationFormatDefinition
    {
        public RemarkP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Remark"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Remark);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Remark_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Directive)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Directive)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class DirectiveP : ClassificationFormatDefinition
    {
        public DirectiveP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Directive"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Directive);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Directive_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Jump)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Jump)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class JumpP : ClassificationFormatDefinition
    {
        public JumpP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Jump"; //human readable version of the name
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Jump);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Jump_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Label)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Label)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class LabelP : ClassificationFormatDefinition
    {
        public LabelP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Label"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
            this.IsItalic = Settings.Default.SyntaxHighlighting_Label_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.LabelDef)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.LabelDef)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class LabelDefP : ClassificationFormatDefinition
    {
        public LabelDefP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Label Definition"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
            this.IsItalic = Settings.Default.SyntaxHighlighting_Label_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Constant)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Constant)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class ConstantP : ClassificationFormatDefinition
    {
        public ConstantP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Constant"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Constant);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Constant_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Misc)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.Misc)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class MiscP : ClassificationFormatDefinition
    {
        public MiscP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Misc"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Misc);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Misc_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.UserDefined1)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.UserDefined1)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class UserDefined1P : ClassificationFormatDefinition
    {
        public UserDefined1P()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Userdefined 1"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined1);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined1_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.UserDefined2)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.UserDefined2)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class UserDefined2P : ClassificationFormatDefinition
    {
        public UserDefined2P()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Userdefined 2"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined2);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined2_Italic;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.UserDefined3)]
    [Name(AsmClassificationDefinition.ClassificationTypeNames.UserDefined3)]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class UserDefined3P : ClassificationFormatDefinition
    {
        public UserDefined3P()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Userdefined 3"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined3);
            this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined3_Italic;
        }
    }
}
