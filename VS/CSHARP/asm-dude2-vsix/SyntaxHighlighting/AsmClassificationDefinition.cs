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

namespace AsmDude2.SyntaxHighlighting
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Utilities;

    internal static class AsmClassificationDefinition
    {
        internal static class ClassificationTypeNames
        {
            public const string Mnemonic = "mnemonic-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string MnemonicOff = "mnemonicOff-65C24A95-28E9-4141-802D-A40A3FA1081A";
            public const string Register = "register-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Remark = "remark-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Directive = "directive-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Constant = "constant-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Jump = "jump-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Label = "label-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string LabelDef = "labelDef-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Misc = "misc-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string UserDefined1 = "userDefined1-E1A959F6-C591-4B22-ADB3-C5C85BAA0B81";
            public const string UserDefined2 = "userDefined2-15067A69-A22F-4092-8BEA-FDF985728446";
            public const string UserDefined3 = "userDefined3-80CA80F7-545B-4DA1-B031-8FBA5B9B2126";
        }

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Mnemonic)]
        internal static ClassificationTypeDefinition Mnemonic = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.MnemonicOff)]
        internal static ClassificationTypeDefinition MnemonicOff = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Register)]
        internal static ClassificationTypeDefinition Register = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Remark)]
        internal static ClassificationTypeDefinition Remark = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Directive)]
        internal static ClassificationTypeDefinition Directive = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Constant)]
        internal static ClassificationTypeDefinition Constant = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Jump)]
        internal static ClassificationTypeDefinition Jump = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Label)]
        internal static ClassificationTypeDefinition Label = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.LabelDef)]
        internal static ClassificationTypeDefinition LabelDef = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Misc)]
        internal static ClassificationTypeDefinition Misc = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.UserDefined1)]
        internal static ClassificationTypeDefinition UserDefined1 = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.UserDefined2)]
        internal static ClassificationTypeDefinition UserDefined2 = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.UserDefined3)]
        internal static ClassificationTypeDefinition UserDefined3 = null;
    }
}
