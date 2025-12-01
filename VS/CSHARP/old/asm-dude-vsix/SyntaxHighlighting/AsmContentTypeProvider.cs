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

/*
 * existing contentTypes
 *
INFO: AsmTaggerProvider:CreateTagger: contentType=UNKNOWN
INFO: AsmTaggerProvider:CreateTagger: contentType=Roslyn Languages
INFO: AsmTaggerProvider:CreateTagger: contentType=TypeScript
INFO: AsmTaggerProvider:CreateTagger: contentType=sighelp
INFO: AsmTaggerProvider:CreateTagger: contentType=TypeScript Signature Help
INFO: AsmTaggerProvider:CreateTagger: contentType=code
INFO: AsmTaggerProvider:CreateTagger: contentType=asm!
INFO: AsmTaggerProvider:CreateTagger: contentType=text
INFO: AsmTaggerProvider:CreateTagger: contentType=Interactive Content
INFO: AsmTaggerProvider:CreateTagger: contentType=projection
INFO: AsmTaggerProvider:CreateTagger: contentType=Interactive Output
INFO: AsmTaggerProvider:CreateTagger: contentType=Interactive Command
INFO: AsmTaggerProvider:CreateTagger: contentType=CSharp
INFO: AsmTaggerProvider:CreateTagger: contentType=CSharp Signature Help
INFO: AsmTaggerProvider:CreateTagger: contentType=RoslynPreviewContentType
INFO: AsmTaggerProvider:CreateTagger: contentType=Basic
INFO: AsmTaggerProvider:CreateTagger: contentType=Basic Signature Help
INFO: AsmTaggerProvider:CreateTagger: contentType=InBoxPowerShell
INFO: AsmTaggerProvider:CreateTagger: contentType=JavaScript
INFO: AsmTaggerProvider:CreateTagger: contentType=ResJSON
INFO: AsmTaggerProvider:CreateTagger: contentType=code++
INFO: AsmTaggerProvider:CreateTagger: contentType=BreakpointFilterExpression
INFO: AsmTaggerProvider:CreateTagger: contentType=Python
INFO: AsmTaggerProvider:CreateTagger: contentType=htmlx
INFO: AsmTaggerProvider:CreateTagger: contentType=Django Templates
INFO: AsmTaggerProvider:CreateTagger: contentType=DjangoTemplateTag
INFO: AsmTaggerProvider:CreateTagger: contentType=intellisense
INFO: AsmTaggerProvider:CreateTagger: contentType=sighelp-doc
INFO: AsmTaggerProvider:CreateTagger: contentType=any
INFO: AsmTaggerProvider:CreateTagger: contentType=plaintext
INFO: AsmTaggerProvider:CreateTagger: contentType=inert
INFO: AsmTaggerProvider:CreateTagger: contentType=Specialized CSharp and VB Interactive Command
INFO: AsmTaggerProvider:CreateTagger: contentType=quickinfo
INFO: AsmTaggerProvider:CreateTagger: contentType=Output
INFO: AsmTaggerProvider:CreateTagger: contentType=ConsoleOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=FindResults
INFO: AsmTaggerProvider:CreateTagger: contentType=Command
INFO: AsmTaggerProvider:CreateTagger: contentType=Immediate
INFO: AsmTaggerProvider:CreateTagger: contentType=snippet picker
INFO: AsmTaggerProvider:CreateTagger: contentType=C/C++
INFO: AsmTaggerProvider:CreateTagger: contentType=ENC
INFO: AsmTaggerProvider:CreateTagger: contentType=Fortran
INFO: AsmTaggerProvider:CreateTagger: contentType=HTML
INFO: AsmTaggerProvider:CreateTagger: contentType=Memory
INFO: AsmTaggerProvider:CreateTagger: contentType=Register
INFO: AsmTaggerProvider:CreateTagger: contentType=T-SQL90
INFO: AsmTaggerProvider:CreateTagger: contentType=VBScript
INFO: AsmTaggerProvider:CreateTagger: contentType=XAML
INFO: AsmTaggerProvider:CreateTagger: contentType=XML
INFO: AsmTaggerProvider:CreateTagger: contentType=XOML
INFO: AsmTaggerProvider:CreateTagger: contentType=TFSourceControlOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=BuildOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=BuildOrderOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=DatabaseOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=TestsOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=SourceControlOutput
INFO: AsmTaggerProvider:CreateTagger: contentType=DebugOutput
*/

namespace AsmDude.SyntaxHighlighting
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Utilities;

    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [Name("AsmDudeContentTypeProvider")]
    internal static class AsmContentTypeProvider
    {
        [Export]
        [Name("asm!")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition AsmContentType = null;

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType = null;

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_cod = null;

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_inc = null;

        [Export]
        [FileExtension(".s")]
        [ContentType(AsmDudePackage.AsmDudeContentType)]
        internal static FileExtensionToContentTypeDefinition AsmFileType_s = null;
    }
}
