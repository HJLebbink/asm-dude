// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
// Port from Python intel-doc-2-md project to C#
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

using System;

namespace AsmAnnotate
{
    /// <summary>
    /// Port of intel-doc-2-md Python project to C#.
    /// Parses Intel PDF documents and extracts instruction definitions.
    ///
    /// Key Implementation Details:
    /// - Text coordinates use (x0, x1, y0, y1) bounding boxes
    /// - Y-coordinates increase upward (typical PDF coordinate system)
    /// - Sorting is done in reverse Y order (top of page first)
    /// - UTF-8 special characters are preserved (em-dash, bullets, etc.)
    /// - Text stripping removes leading/trailing whitespace but preserves internal spacing
    /// </summary>

    /// <summary>
    /// Represents a single PDF text element with coordinates and content.
    /// Corresponds to pdfminer's LTTextLineHorizontal.
    /// </summary>
    public class PdfTextElement
    {
        /// <summary>
        /// The raw text content from PDF
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Left edge X coordinate
        /// </summary>
        public double X0 { get; set; }

        /// <summary>
        /// Right edge X coordinate
        /// </summary>
        public double X1 { get; set; }

        /// <summary>
        /// Bottom edge Y coordinate
        /// </summary>
        public double Y0 { get; set; }

        /// <summary>
        /// Top edge Y coordinate
        /// </summary>
        public double Y1 { get; set; }

        /// <summary>
        /// Height of the text element (Y1 - Y0)
        /// </summary>
        public double Height => Y1 - Y0;

        /// <summary>
        /// Width of the text element (X1 - X0)
        /// </summary>
        public double Width => X1 - X0;

        /// <summary>
        /// Font name (e.g., "NeoSansIntelMedium")
        /// </summary>
        public string FontName { get; set; } = string.Empty;

        /// <summary>
        /// Returns trimmed content
        /// </summary>
        public string GetText() => Content?.Trim() ?? string.Empty;

        public override string ToString() => GetText();
    }


    /// <summary>
    /// Represents a line (vector graphics, not text).
    /// Can be vertical or horizontal based on width/height.
    /// </summary>
    public class PdfLineElement
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }

        public double Height => Y1 - Y0;
        public double Width => X1 - X0;

        /// <summary>
        /// Vertical lines have width < 1.0
        /// </summary>
        public bool IsVertical => Math.Abs(Width) < 1.0;

        /// <summary>
        /// Horizontal lines have height < 1.0
        /// </summary>
        public bool IsHorizontal => Math.Abs(Height) < 1.0;
    }


    /// <summary>
    /// Tracks state during markdown generation from piles.
    /// Equivalent to the State class in Python's writer.py.
    /// </summary>
    public class MarkdownState
    {
        /// <summary>
        /// Whether we are currently in a code block (```...```)
        /// </summary>
        public bool CodeMode { get; set; } = false;

        /// <summary>
        /// Current section type: title, description, encoding, operation, flags, exceptions, intrinsics, etc.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Next section type (used for state transitions)
        /// </summary>
        public string? TypeNext { get; set; }

        /// <summary>
        /// Whether the previous pile was an opcode table
        /// </summary>
        public bool PreviousPileIsOpcodeTable { get; set; } = false;

        /// <summary>
        /// Whether the current pile is an opcode table
        /// </summary>
        public bool CurrentPileIsOpcodeTable { get; set; } = false;

        /// <summary>
        /// Whether the next pile is an opcode table
        /// </summary>
        public bool NextPileIsOpcodeTable { get; set; } = false;
    }


    /// <summary>
    /// Helper class for itext7 text extraction results
    /// </summary>
    internal class PdfTextInfo
    {
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string FontName { get; set; } = string.Empty;
    }


    /// <summary>
    /// Helper class for itext7 line extraction results (vector graphics)
    /// </summary>
    internal class PdfLineInfo
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }
}
