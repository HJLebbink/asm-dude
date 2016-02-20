// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Collections.ObjectModel;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude {
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("asm!")]
    [Name("asmCompletion")]
    class AsmCompletionSourceProvider : ICompletionSourceProvider {
        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer) {
            return new AsmCompletionSource(textBuffer);
        }
    }

    class AsmCompletionSource : ICompletionSource {
        private ITextBuffer _buffer;
        private bool _disposed = false;

        public AsmCompletionSource(ITextBuffer buffer) {
            _buffer = buffer;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            if (_disposed)
                throw new ObjectDisposedException("AsmCompletionSource");

            List<Completion> completions = new List<Completion>()
            {
                //new Completion("Ook!"),
                //new Completion("Ook."),
                //new Completion("Ook?")
            };

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(snapshot);

            if (triggerPoint == null)
                return;

            var line = triggerPoint.GetContainingLine();
            SnapshotPoint start = triggerPoint;

            while (start > line.Start && !char.IsWhiteSpace((start - 1).GetChar())) {
                start -= 1;
            }

            var applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

            completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
        }

        public void Dispose() {
            _disposed = true;
        }
    }
}

