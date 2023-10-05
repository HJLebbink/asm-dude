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

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace inteldoc
{
    using PdfSharp.Pdf.Content.Objects;
    using PdfSharp.Pdf.Content;

    using System;

    using PdfSharp.Pdf.IO;
    using PdfSharp.Pdf;
    using intel_doc_2_md;

    internal class IntelDocMain
    {
        [STAThread]
        private static void Main()
        {
//            List<(int from, int to)> pageRanges = new();
//            pageRanges.Add((128, 129));

            PdfDocument doc = PdfReader.Open(Path.Combine("resources", "325383-sdm-vol-2abcd june 2023.pdf"), PdfDocumentOpenMode.ReadOnly);

            string extractedText = ExtractTextClass.GetTextPage(doc, 128);
            Console.WriteLine(extractedText);
        }
    }
}


