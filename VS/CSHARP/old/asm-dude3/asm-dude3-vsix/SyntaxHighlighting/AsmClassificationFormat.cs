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

namespace AsmDude3.SyntaxHighlighting
{
    using System.ComponentModel.Composition;
    using AsmDude3.Tools;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Classification format definitions for assembly language syntax highlighting.
    /// These map the logical classification types (from AsmClassificationDefinition)
    /// to visual formats (colors, font styles, etc.) based on user settings.
    /// </summary>
    public static class AsmClassificationFormat
    {
        #region Mnemonic / Opcode Formats

        /// <summary>
        /// Format for mnemonic (opcode) tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "mnemonic-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Mnemonic")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class MnemonicFormatDefinition : ClassificationFormatDefinition
        {
            public MnemonicFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Mnemonic";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Opcode);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Opcode_Italic;
            }
        }

        /// <summary>
        /// Format for mnemonic tokens when the instruction is disabled/unsupported.
        /// Rendered with reduced opacity (0.4) to indicate it's not available.
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "mnemonicOff-65C24A95-28E9-4141-802D-A40A3FA1081A")]
        [Name("AsmDude - Mnemonic (Off)")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class MnemonicOffFormatDefinition : ClassificationFormatDefinition
        {
            public MnemonicOffFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Mnemonic (Disabled)";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Opcode);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Opcode_Italic;
                this.ForegroundOpacity = 0.4; // 40% opacity to show it's disabled
            }
        }

        #endregion

        #region Register Format

        /// <summary>
        /// Format for register tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "register-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Register")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class RegisterFormatDefinition : ClassificationFormatDefinition
        {
            public RegisterFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Register";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Register);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Register_Italic;
            }
        }

        #endregion

        #region Remark / Comment Format

        /// <summary>
        /// Format for remark (comment) tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "remark-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Remark")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class RemarkFormatDefinition : ClassificationFormatDefinition
        {
            public RemarkFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Remark";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Remark);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Remark_Italic;
            }
        }

        #endregion

        #region Directive Format

        /// <summary>
        /// Format for assembly directive tokens (e.g., .section, .globl) with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "directive-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Directive")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class DirectiveFormatDefinition : ClassificationFormatDefinition
        {
            public DirectiveFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Directive";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Directive);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Directive_Italic;
            }
        }

        #endregion

        #region Jump / Branch Format

        /// <summary>
        /// Format for jump/branch instruction tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "jump-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Jump")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class JumpFormatDefinition : ClassificationFormatDefinition
        {
            public JumpFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Jump";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Jump);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Jump_Italic;
            }
        }

        #endregion

        #region Label Formats

        /// <summary>
        /// Format for label reference tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "label-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Label")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class LabelFormatDefinition : ClassificationFormatDefinition
        {
            public LabelFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Label";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Label_Italic;
            }
        }

        /// <summary>
        /// Format for label definition tokens (where label is defined, typically with colon)
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "labelDef-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Label Definition")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class LabelDefFormatDefinition : ClassificationFormatDefinition
        {
            public LabelDefFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Label Definition";
                // Typically same color as label references for consistency
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Label);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Label_Italic;
            }
        }

        #endregion

        #region Constant Format

        /// <summary>
        /// Format for constant/numeric literal tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "constant-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Constant")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class ConstantFormatDefinition : ClassificationFormatDefinition
        {
            public ConstantFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Constant";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Constant);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Constant_Italic;
            }
        }

        #endregion

        #region Miscellaneous Format

        /// <summary>
        /// Format for miscellaneous tokens with user-configured color and italic
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "misc-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        [Name("AsmDude - Miscellaneous")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class MiscFormatDefinition : ClassificationFormatDefinition
        {
            public MiscFormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - Miscellaneous";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Misc);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Misc_Italic;
            }
        }

        #endregion

        #region User-Defined Formats

        /// <summary>
        /// Format for user-defined token type 1 (customizable by user)
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "userDefined1-E1A959F6-C591-4B22-ADB3-C5C85BAA0B81")]
        [Name("AsmDude - User-Defined 1")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class UserDefined1FormatDefinition : ClassificationFormatDefinition
        {
            public UserDefined1FormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - User-Defined 1";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined1);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined1_Italic;
            }
        }

        /// <summary>
        /// Format for user-defined token type 2 (customizable by user)
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "userDefined2-15067A69-A22F-4092-8BEA-FDF985728446")]
        [Name("AsmDude - User-Defined 2")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class UserDefined2FormatDefinition : ClassificationFormatDefinition
        {
            public UserDefined2FormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - User-Defined 2";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined2);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined2_Italic;
            }
        }

        /// <summary>
        /// Format for user-defined token type 3 (customizable by user)
        /// </summary>
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "userDefined3-80CA80F7-545B-4DA1-B031-8FBA5B9B2126")]
        [Name("AsmDude - User-Defined 3")]
        [UserVisible(true)]
        [Order(Before = Priority.Default)]
        public sealed class UserDefined3FormatDefinition : ClassificationFormatDefinition
        {
            public UserDefined3FormatDefinition()
            {
                this.DisplayName = "AsmDude - Syntax Highlighting - User-Defined 3";
                this.ForegroundColor = AsmDudeToolsStatic.ConvertColor(Settings.Default.SyntaxHighlighting_Userdefined3);
                this.IsItalic = Settings.Default.SyntaxHighlighting_Userdefined3_Italic;
            }
        }

        #endregion
    }
}
