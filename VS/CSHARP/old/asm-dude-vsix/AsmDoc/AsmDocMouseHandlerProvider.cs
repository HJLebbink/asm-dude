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

namespace AsmDude.AsmDoc
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IMouseProcessorProvider))]
    [ContentType(AsmDudePackage.AsmDudeContentType)]
    [ContentType(AsmDudePackage.DisassemblyContentType)]
    [Name("AsmDocMouseHandlerProvider")]
    [TextViewRole(PredefinedTextViewRoles.Debuggable)] // necessary for disassembly window
    [Order(Before = "WordSelection")]
    internal sealed class AsmDocMouseHandlerProvider : IMouseProcessorProvider
    {
        [Import]
        private readonly IBufferTagAggregatorFactoryService aggregatorFactory_ = null;

        public IMouseProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            return new AsmDocMouseHandler(
                view,
                this.aggregatorFactory_,
                CtrlKeyState.GetStateForView(view),
                AsmDudeTools.Instance);
        }
    }
}
