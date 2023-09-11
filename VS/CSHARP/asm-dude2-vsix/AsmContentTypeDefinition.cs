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

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace AsmDude2
{
    public class AsmContentDefinition
    {
        [Export]
        [Name("asm!")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
#pragma warning disable CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
        internal static ContentTypeDefinition asmContentTypeDefinition;
#pragma warning restore CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null

        [Export]
        [FileExtension(".asm")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
#pragma warning disable CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition;
#pragma warning restore CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null

        [Export]
        [FileExtension(".cod")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
#pragma warning disable CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_cod;
#pragma warning restore CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null

        [Export]
        [FileExtension(".inc")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
#pragma warning disable CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_inc;
#pragma warning restore CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null

        [Export]
        [FileExtension(".s")]
        [ContentType(AsmDude2Package.AsmDudeContentType)]
#pragma warning disable CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
        internal static FileExtensionToContentTypeDefinition asmFileExtensionDefinition_s;
#pragma warning restore CS0649 // Field 'AsmContentDefinition.asmContentTypeDefinition' is never assigned to, and will always have its default value null
    }
}
