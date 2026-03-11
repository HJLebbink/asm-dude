// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
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

namespace AsmDude2.CodeLens
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class AsmCodeLensProvider : IWpfTextViewCreationListener
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(AsmCodeLensAdornmentManager.AdornmentLayerName)]
        [Order(After = PredefinedAdornmentLayers.Text)]
#pragma warning disable CS0169 // Field is used by MEF via Export attribute
        private AdornmentLayerDefinition editorAdornmentLayer;
#pragma warning restore CS0169

        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(typeof(AsmCodeLensAdornmentManager), () => new AsmCodeLensAdornmentManager(textView));
        }
    }

    [Export(typeof(ILineTransformSourceProvider))]
    [ContentType(AsmDude2Package.AsmDudeContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class AsmCodeLensLineTransformSourceProvider : ILineTransformSourceProvider
    {
        [Import]
        internal IClassificationFormatMapService ClassificationFormatMapService { get; set; }

        public ILineTransformSource Create(IWpfTextView textView)
        {
            IClassificationFormatMap formatMap = this.ClassificationFormatMapService.GetClassificationFormatMap(textView);
            return textView.Properties.GetOrCreateSingletonProperty(() => new AsmCodeLensLineTransformSource(textView, formatMap));
        }
    }
}
