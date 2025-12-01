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
    using System.Windows.Input;
    using Microsoft.VisualStudio.Text.Editor;

    /// <summary>
    /// Listen for the control key being pressed or released to update the CtrlKeyStateChanged for a view.
    /// </summary>
    internal sealed class AsmDocKeyProcessor : KeyProcessor
    {
        private readonly CtrlKeyState state_;

        public AsmDocKeyProcessor(CtrlKeyState state)
        {
            this.state_ = state;
        }

        private void UpdateState(KeyEventArgs args)
        {
            this.state_.Enabled =
                ((args.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) &&
                ((args.KeyboardDevice.Modifiers & ModifierKeys.Shift) == 0);
        }

        public override void PreviewKeyDown(KeyEventArgs args)
        {
            this.UpdateState(args);
        }

        public override void PreviewKeyUp(KeyEventArgs args)
        {
            this.UpdateState(args);
        }
    }
}
