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

using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude.SyntaxHighlighting
{
    internal static class AsmClassificationDefinition
    {
        internal static class ClassificationTypeNames
        {
            public const string Mnemonic = "mnemonic-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Register = "register-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Remark = "remark-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Directive = "directive-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Constant = "constant-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Jump = "jump-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Label = "label-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string LabelDef = "labelDef-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
            public const string Misc = "misc-D74860FA-F0BC-4441-9D76-DF4ECB19CF71";
        }

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Mnemonic)]
        internal static ClassificationTypeDefinition mnemonic = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Register)]
        internal static ClassificationTypeDefinition register = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Remark)]
        internal static ClassificationTypeDefinition remark = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Directive)]
        internal static ClassificationTypeDefinition directive = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Constant)]
        internal static ClassificationTypeDefinition constant = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Jump)]
        internal static ClassificationTypeDefinition jump = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Label)]
        internal static ClassificationTypeDefinition label = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.LabelDef)]
        internal static ClassificationTypeDefinition labelDef = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(ClassificationTypeNames.Misc)]
        internal static ClassificationTypeDefinition misc = null;
    }
}
