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
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    /// <summary>
    /// Classification type definitions for assembly language syntax highlighting.
    /// These define the logical classification types, which are then mapped to visual
    /// formats by AsmClassificationFormat.cs
    /// </summary>
    public static class AsmClassificationDefinition
    {
        /// <summary>
        /// Classification for mnemonic (opcode) tokens
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("mnemonic-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition MnemonicClassificationType = null;

        /// <summary>
        /// Classification for mnemonic tokens when the instruction is disabled/unsupported
        /// Rendered with reduced opacity (0.4)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("mnemonicOff-65C24A95-28E9-4141-802D-A40A3FA1081A")]
        public static ClassificationTypeDefinition MnemonicOffClassificationType = null;

        /// <summary>
        /// Classification for register tokens
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("register-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition RegisterClassificationType = null;

        /// <summary>
        /// Classification for remark (comment) tokens
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("remark-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition RemarkClassificationType = null;

        /// <summary>
        /// Classification for assembly directive tokens (e.g., .section, .globl)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("directive-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition DirectiveClassificationType = null;

        /// <summary>
        /// Classification for jump/branch instruction tokens
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("jump-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition JumpClassificationType = null;

        /// <summary>
        /// Classification for label reference tokens (when label is used, not defined)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("label-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition LabelClassificationType = null;

        /// <summary>
        /// Classification for label definition tokens (e.g., "label_name:" at start of line)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("labelDef-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition LabelDefClassificationType = null;

        /// <summary>
        /// Classification for constant/numeric literal tokens
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("constant-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition ConstantClassificationType = null;

        /// <summary>
        /// Classification for miscellaneous tokens that don't fit other categories
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("misc-D74860FA-F0BC-4441-9D76-DF4ECB19CF71")]
        public static ClassificationTypeDefinition MiscClassificationType = null;

        /// <summary>
        /// Classification for user-defined token type 1 (customizable by user)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("userDefined1-E1A959F6-C591-4B22-ADB3-C5C85BAA0B81")]
        public static ClassificationTypeDefinition UserDefined1ClassificationType = null;

        /// <summary>
        /// Classification for user-defined token type 2 (customizable by user)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("userDefined2-15067A69-A22F-4092-8BEA-FDF985728446")]
        public static ClassificationTypeDefinition UserDefined2ClassificationType = null;

        /// <summary>
        /// Classification for user-defined token type 3 (customizable by user)
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("userDefined3-80CA80F7-545B-4DA1-B031-8FBA5B9B2126")]
        public static ClassificationTypeDefinition UserDefined3ClassificationType = null;
    }
}
