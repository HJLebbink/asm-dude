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
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;


namespace AsmDude {

    /// <summary>
    /// Defines the editor format for the mnemonic classification type. Text is colored Blue
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "mnemonic")]
    [Name("mnemonic")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class OperandP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "opcode" classification type
        /// </summary>
        public OperandP() {
            DisplayName = "mnemonic"; //human readable version of the name
            ForegroundColor = Colors.Blue;
        }
    }

    /// <summary>
    /// Defines the editor format for the register classification type. Text is colored DarkRed
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "register")]
    [Name("register")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class RegisterP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "register" classification type
        /// </summary>
        public RegisterP() {
            DisplayName = "register"; //human readable version of the name
            ForegroundColor = Colors.DarkRed;
        }
    }

    /// <summary>
    /// Defines the editor format for the remark classification type. Text is colored Green
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "remark")]
    [Name("remark")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class RemarkP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "remark" classification type
        /// </summary>
        public RemarkP() {
            DisplayName = "remark"; //human readable version of the name
            ForegroundColor = Colors.Green;
            IsItalic = true;
        }
    }

    /// <summary>
    /// Defines the editor format for the directive classification type. Text is colored Magenta
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "directive")]
    [Name("directive")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class DirectiveP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "directive" classification type
        /// </summary>
        public DirectiveP() {
            DisplayName = "directive"; //human readable version of the name
            ForegroundColor = Colors.Magenta;
            IsItalic = true;
        }
    }

    /// <summary>
    /// Defines the editor format for the jump classification type. Text is colored Navy
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "jump")]
    [Name("jump")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class JumpP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "jump" classification type
        /// </summary>
        public JumpP() {
            DisplayName = "jump"; //human readable version of the name
            ForegroundColor = Colors.Navy;
        }
    }

    /// <summary>
    /// Defines the editor format for the label classification type. Text is colored OrangeRed
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "label")]
    [Name("label")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class LabelP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "jump" classification type
        /// </summary>
        public LabelP() {
            DisplayName = "Display label"; //human readable version of the name
            ForegroundColor = Colors.OrangeRed;
        }
    }

    /// <summary>
    /// Defines the editor format for the constant classification type. Text is colored OrangeRed
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "constant")]
    [Name("constant")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class ConstantP : ClassificationFormatDefinition {
        /// <summary>
        /// Defines the visual format for the "jump" classification type
        /// </summary>
        public ConstantP() {
            DisplayName = "constant"; //human readable version of the name
            ForegroundColor = Colors.Chocolate;
        }
    }

    /// <summary>
    /// Defines the editor format for the misc classification type. Text is colored DarkOrange
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "misc")]
    [Name("misc")]
    //this should be visible to the end user
    [UserVisible(false)]
    //set the priority to be after the default classifiers
    [Order(Before = Priority.Default)]
    internal sealed class MiscP : ClassificationFormatDefinition
    {
        /// <summary>
        /// Defines the visual format for the "jump" classification type
        /// </summary>
        public MiscP()
        {
            DisplayName = "misc"; //human readable version of the name
            ForegroundColor = Colors.DarkOrange;
        }
    }
}
