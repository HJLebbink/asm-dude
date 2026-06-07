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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Path = iText.Kernel.Geom.Path;

namespace AsmAnnotate
{

    /// <summary>
    /// Generates markdown files from ContentPile objects.
    /// Equivalent to the Writer class in Python's writer.py.
    /// Handles instruction boundaries and opcode table merging.
    /// </summary>
    public class MarkdownGenerator
    {
        private readonly string _sourceInfo;
        private readonly string _outputDirectory;

        public MarkdownGenerator(string outputDirectory = "./output", string sourceInfo = "Intel® 64 and IA-32 Architectures Software Developer's Manual, Combined Volumes (Order Number 325462)")
        {
            _outputDirectory = outputDirectory;
            _sourceInfo = sourceInfo;
            Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Processes a list of piles and generates one markdown file per instruction.
        /// </summary>
        public void Write(List<ContentPile> piles)
        {
            if (piles.Count == 0) return;

            string instructionCurrent = null;
            string descriptionCurrent = null;
            (instructionCurrent, descriptionCurrent) = piles[0].GetInstruction();

            string instructionPrev = instructionCurrent;

            var state = new MarkdownState();
            var markdown = new StringBuilder();

            for (int i = 0; i < piles.Count; i++)
            {
                var pile = piles[i];
                var (pileInstruction, pileDescription) = pile.GetInstruction();

                bool createNewFile = false;
                if (pileInstruction != null && pileInstruction != instructionCurrent)
                {
                    createNewFile = true;
                    instructionPrev = instructionCurrent;
                    instructionCurrent = pileInstruction;
                }

                state.CurrentPileIsOpcodeTable = pile.IsOpcodeTable();
                if (state.CurrentPileIsOpcodeTable)
                {
                    state.PreviousPileIsOpcodeTable = FindPreviousOpcodeTable(i, piles, instructionCurrent);
                    state.NextPileIsOpcodeTable = FindNextOpcodeTable(i, piles, instructionCurrent);
                }
                else
                {
                    state.PreviousPileIsOpcodeTable = false;
                    state.NextPileIsOpcodeTable = false;
                }

                if (createNewFile)
                {
                    CloseFile(instructionPrev, markdown.ToString());
                    markdown.Clear();
                    state.PreviousPileIsOpcodeTable = false;
                }

                markdown.Append(pile.GenerateMarkdown(state));
            }

            CloseFile(instructionCurrent, markdown.ToString());
        }

        /// <summary>
        /// Finds whether the opcode table immediately preceding this one (with no title in between)
        /// is also an opcode table — i.e. the two should be merged into one HTML table.
        /// Stops at ANY title pile: a different instruction's title obviously ends the search, but
        /// so does the CURRENT instruction's own title — nothing before that belongs to this
        /// instruction. Without the latter stop the scan runs into the PREVIOUS instruction's
        /// opcode table and wrongly merges, dropping this table's header row and "&lt;table&gt;" tag
        /// (the FBLD/FDIV/... bug).
        /// </summary>
        private bool FindPreviousOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex - 1; j >= 0; j--)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null)
                {
                    return false; // reached a title (this instruction's or another's) — no merge across it
                }

                if (pile.Kind == PileKind.Table)
                {
                    return pile.IsOpcodeTable();
                }
            }

            return false;
        }

        /// <summary>
        /// Finds whether the opcode table immediately following this one (no title in between) is
        /// also an opcode table — the symmetric partner of <see cref="FindPreviousOpcodeTable"/>.
        /// Stops at ANY title pile so an instruction's opcode table never merges with the NEXT
        /// instruction's table.
        /// </summary>
        private bool FindNextOpcodeTable(int currentIndex, List<ContentPile> piles, string instructionCurrent)
        {
            for (int j = currentIndex + 1; j < piles.Count; j++)
            {
                var pile = piles[j];
                var (pileInstruction, _) = pile.GetInstruction();

                if (pileInstruction != null)
                {
                    return false; // reached a title — no merge across it
                }

                if (pile.Kind == PileKind.Table)
                {
                    return pile.IsOpcodeTable();
                }
            }

            return false;
        }

        /// <summary>
        /// Writes markdown to a file for the given instruction.
        /// Applies hyphenation cleanup and adds generation metadata.
        /// </summary>
        private void CloseFile(string instruction, string markdown)
        {
            if (string.IsNullOrEmpty(instruction)) return;

            // Every real instruction-reference page has an OPCODE table (its header cell reads
            // "Opcode" or "Opcode/Instruction"). A "title" without one is junk caught by the title
            // font: a section heading ("IA-32 Memory Models", "RTM — Enabled Debugger Support",
            // "VM — Exit Controls for MSRs"), an Appendix-B encoding table (header "Instruction and
            // Format"), or a cross-reference stub. Requiring "Opcode" also prevents an Appendix-B
            // page from OVERWRITING the real instruction's file (e.g. the real FYL2X is kept, the
            // appendix FYL2X table is dropped).
            if (!markdown.Contains("Opcode", StringComparison.Ordinal)) return;

            // Normalise to LF FIRST. StringBuilder.AppendLine emits CRLF on Windows, but the
            // CleanupHyphenation patterns are written with '\n' (e.g. "oper-\nands"); on CRLF text
            // they would never match and the broken words ("oper- ands") would survive. Also strip
            // any stray BOM/zero-width char. (Do NOT collapse blank runs — the reference puts two
            // blank lines before some section headers, e.g. "</table>\n\n\n### ...".)
            markdown = markdown.Replace("\r\n", "\n").Replace("﻿", "");

            // Re-join words the PDF hyphenated across a line break (curated list, as in the Python).
            markdown = TextCleaner.CleanupHyphenation(markdown);

            string safeFileName = instruction.Replace("/", "_").Replace(" ", "_");
            // Strip any remaining characters that are illegal in Windows file names
            // (e.g. a stray ':' '>' '*' from an oddly-formatted heading) to avoid IOExceptions.
            foreach (char bad in System.IO.Path.GetInvalidFileNameChars())
                safeFileName = safeFileName.Replace(bad, '_');
            string filePath = System.IO.Path.Combine(_outputDirectory, $"{safeFileName}.md");

            var now = DateTime.Now;
            string generatedTime = $"{now.Day}-{now.Month}-{now.Year}";
            markdown += $"\n --- \n<p align=\"right\"><i>Source: {_sourceInfo}<br>Generated: {generatedTime}</i></p>\n";

            // UTF-8 without a BOM (the reference files have no BOM).
            var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.WriteLine($"Writing {filePath}");
            File.WriteAllText(filePath, markdown, utf8NoBom);
        }
    }
}
