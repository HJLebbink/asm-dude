// The MIT License (MIT)
//
// Copyright (c) 2016 H.J. Lebbink
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

namespace AsmDude {

    [Export(typeof(EditorFormatDefinition))] // export as EditorFormatDefinition otherwise the syntax coloring does not work
    [ClassificationType(ClassificationTypeNames = "mnemonic")]
    [Name("mnemonic")]  //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class OpcodeP : ClassificationFormatDefinition {
        
        public OpcodeP() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            DisplayName = "mnemonic"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Opcode);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "register")]
    [Name("register")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class RegisterP : ClassificationFormatDefinition {
        public RegisterP() {
            DisplayName = "register"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Register);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "remark")]
    [Name("remark")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class RemarkP : ClassificationFormatDefinition {
        public RemarkP() {
            DisplayName = "remark"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Remark);
            IsItalic = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "directive")]
    [Name("directive")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class DirectiveP : ClassificationFormatDefinition {
        public DirectiveP() {
            DisplayName = "directive"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Directive);
            IsItalic = true;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "jump")]
    [Name("jump")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class JumpP : ClassificationFormatDefinition {
        public JumpP() {
            DisplayName = "jump"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Jump);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "label")]
    [Name("label")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class LabelP : ClassificationFormatDefinition {
        public LabelP() {
            DisplayName = "Display label"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "labelDef")]
    [Name("labelDef")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class LabelDefP : ClassificationFormatDefinition {
        public LabelDefP() {
            DisplayName = "Display label Def"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Label);
            //TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "constant")]
    [Name("constant")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class ConstantP : ClassificationFormatDefinition {
        public ConstantP() {
            DisplayName = "constant"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Constant);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "misc")]
    [Name("misc")] //this should be visible to the end user
    [UserVisible(true)] //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class MiscP : ClassificationFormatDefinition {
        public MiscP() {
            DisplayName = "misc"; //human readable version of the name
            ForegroundColor = AsmDudeToolsStatic.convertColor(Settings.Default.SyntaxHighlighting_Misc);
        }
    }
}
