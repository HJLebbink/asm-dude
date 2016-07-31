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

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;

namespace AsmDude.Tools {

    public interface ILabelGraph {
        SortedSet<uint> getLabelDefLineNumbers(string label);
        IList<int> getAllRelatedLineNumber();

        /// <summary>
        /// Return whether this label graph is enabled
        /// </summary>
        bool isEnabled { get; }

        int getLinenumber(uint id);
        string getFilename(uint id);
        bool isFromMainFile(uint id);


        bool hasLabel(string label);
        bool hasLabelClash(string label);
        SortedSet<uint> labelUsedAtInfo(string label);

        /// <summary>
        /// Return dictionary of line numbers with label clash descriptions
        /// </summary>
        SortedDictionary<uint, string> labelClashes { get; }

        /// <summary>
        /// Return dictionary of line numbers with undefined label descriptions
        /// </summary>
        SortedDictionary<uint, string> undefinedLabels { get; }

        SortedDictionary<string, string> getLabelDescriptions { get; }

        void reset_Delayed();
        event EventHandler<CustomEventArgs> ResetDoneEvent;
    }
}