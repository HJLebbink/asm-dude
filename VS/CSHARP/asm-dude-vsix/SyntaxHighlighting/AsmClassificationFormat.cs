// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using AsmDude.Tools;
using AsmDude.SyntaxHighlighting;

namespace AsmDude
{
    [Export(typeof(EditorFormatDefinition))] // export as EditorFormatDefinition otherwise the syntax coloring does not work
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Mnemonic)]
    [Name("mnemonic-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class OpcodeP : ClassificationFormatDefinition
    {
        public OpcodeP()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            this.DisplayName = "AsmDude - Syntax Highlighting - Mnemonic"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Opcode);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Register)]
    [Name("register-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class RegisterP : ClassificationFormatDefinition
    {
        public RegisterP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Register"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Register);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Remark)]
    [Name("remark-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class RemarkP : ClassificationFormatDefinition
    {
        public RemarkP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Remark"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Remark);
            this.IsItalic = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Directive)]
    [Name("directive-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class DirectiveP : ClassificationFormatDefinition
    {
        public DirectiveP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Directive"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Directive);
            this.IsItalic = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Jump)]
    [Name("jump-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class JumpP : ClassificationFormatDefinition
    {
        public JumpP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Jump"; //human readable version of the name
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Jump);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Label)]
    [Name("label-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class LabelP : ClassificationFormatDefinition
    {
        public LabelP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Label"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.LabelDef)]
    [Name("labelDef-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class LabelDefP : ClassificationFormatDefinition
    {
        public LabelDefP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Label Definition"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Constant)]
    [Name("constant-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class ConstantP : ClassificationFormatDefinition
    {
        public ConstantP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Constant"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Constant);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = AsmClassificationDefinition.ClassificationTypeNames.Misc)]
    [Name("misc-961E99C2-2082-4140-ACBD-966AEDEB60A2")]
    [UserVisible(true)] // sets this editor format definition visible for the user (in Tools>Options>Environment>Fonts and Colors>Text Editor
    [Order(After = Priority.High)] //set the priority to be after the default classifiers
    internal sealed class MiscP : ClassificationFormatDefinition
    {
        public MiscP()
        {
            this.DisplayName = "AsmDude - Syntax Highlighting - Misc"; //human readable version of the name found in Tools>Options>Environment>Fonts and Colors>Text Editor
            this.ForegroundColor = AsmDudeToolsStatic.Convert_Color(Settings.Default.SyntaxHighlighting_Misc);
        }
    }
}
