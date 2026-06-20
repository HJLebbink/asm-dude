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
using System.Text.RegularExpressions;

namespace AsmAnnotate
{

    /// <summary>
    /// Cleans up text extracted from PDF documents.
    /// Handles common PDF artifacts like word breaks and special characters.
    ///
    /// CRITICAL: The hyphenation patterns below are empirically determined
    /// from analyzing multiple Intel PDFs. They handle cases where PDFMiner
    /// preserves line breaks: "instruc-\ntions" appears in the extracted text.
    /// </summary>
    public static class TextCleaner
    {
        /// <summary>
        /// Removes PDF hyphenation artifacts and normalizes text.
        ///
        /// The patterns below are specific to Intel documentation.
        /// PDFs often break words across lines with hyphens, which need to be rejoined.
        ///
        /// Example issues in extracted text:
        /// - "instruc-\ntions" should be "instructions"
        /// - "single- precision" should be "single-precision" (space after hyphen is a PDF artifact)
        /// - "•\n" should be "\n * " (bullet points to markdown)
        /// </summary>
        /// <summary>
        /// Words the Intel PDF splits across a line break where the hyphen is an artifact and must
        /// be DROPPED (a single word, e.g. "excep-tion" -> "exception"). Each entry is the broken
        /// word with its single hyphen at the split point. Real compounds that must KEEP their
        /// hyphen ("floating-point", "general-purpose", "64-bit", "machine-check") are deliberately
        /// NOT listed — the general rule in <see cref="CleanupHyphenation"/> handles those.
        /// Curated from the actual rev-091 SDM output (the Python tool kept an equivalent list).
        /// </summary>
        private static readonly string[] SplitWords =
        [
            // originally ported from the Python writer
            "addi-tional", "combina-tion", "compar-ison", "compar-isons", "corre-sponding",
            "documenta-tion", "destina-tion", "desti-nation", "infor-mation", "instruc-tions",
            "instruc-tion", "regis-ters", "regis-ter", "oper-ands", "preci-sion", "loca-tions",
            "loca-tion", "speci-fied", "unpre-dictable", "priv-ilege",
            // extended from the SDM (combined volumes) output
            "Soft-ware", "soft-ware", "hard-ware", "excep-tion", "excep-tions", "proces-sors",
            "pro-cessor", "inter-rupt", "inter-rupts", "inter-rupted", "Inter-rupt", "oper-ation",
            "oper-ations", "oper-ating", "oper-and", "oper-ates", "opera-tion", "opera-tions",
            "Archi-tectures", "archi-tecture", "archi-tectural", "architec-ture", "architec-tures",
            "architec-tural", "execu-tion", "perfor-mance", "Perfor-mance", "gener-ated",
            "gener-ates", "gener-ation", "gener-ations", "align-ment", "condition-ally",
            "condi-tionally", "respec-tively", "imme-diate", "immedi-ately", "spec-ified",
            "proce-dure", "proce-dures", "deter-mine", "deter-mined", "deter-mines", "moni-toring",
            "indi-cates", "indi-cated", "indi-cate", "avail-able", "appro-priate", "tech-nology",
            "Tech-nology", "interme-diate", "inter-mediate", "inte-gers", "environ-ment",
            "envi-ronment", "compar-ison", "transac-tional", "trans-actional", "struc-ture",
            "struc-tures", "recom-mended", "recom-mends", "other-wise", "func-tion", "func-tions",
            "expo-nent", "differ-ences", "attri-bute", "attri-butes", "write-mask", "double-word",
            "double-words", "quad-word", "unde-fined", "subse-quent", "reg-ister", "phys-ical",
            "partic-ular", "microarchi-tecture", "microarchitec-ture", "microar-chitecture",
            "mecha-nism", "mecha-nisms", "mech-anism", "initializa-tion", "initial-ized",
            "exten-sions", "exten-sion", "auto-matically", "applica-tion", "applica-tions",
            "appli-cation", "appli-cable", "under-flow", "over-flow", "over-flows", "refer-ences",
            "refer-ence", "incre-ments", "incre-mented", "incor-rect", "imple-mented",
            "imple-mentation", "imple-mentations", "condi-tions", "compo-nents", "compo-nent",
            "compati-bility", "associ-ated", "asso-ciated", "Optimi-zation", "optimi-zation",
            "optimiza-tions", "transi-tions", "transla-tion", "trans-lation", "signifi-cand",
            "repre-sented", "prop-erly", "opti-mized", "neces-sary", "modi-fied", "logi-cal",
            "depen-dent", "config-ured", "config-uration", "capa-bilities", "arith-metic",
            "Specifi-cally", "virtu-alization", "viola-tions", "subtrac-tion", "sema-phore",
            "prob-lems", "plat-form", "out-side", "magni-tude", "identifi-cation", "iden-tify",
            "illus-trated", "hier-archy", "granu-larity", "exec-utive", "distin-guished",
            "defini-tions", "defi-nition", "compu-tations", "circum-stances", "authenti-cated",
            "allo-cated", "accom-plished", "acces-sible", "There-fore", "band-width", "gath-ered",
            "inter-face", "Devel-oper",
            // added from a rev-091 corpus scan of the table-cell descriptions
            "ele-ments", "val-ues", "han-dle", "permit-ted", "per-mitted", "mes-sage", "descrip-tor",
            "mem-ory", "fea-ture", "sig-naling", "nonsig-naling", "nonsignal-ing", "ver-sion",
            "regis-ter", "regis-ters", "comput-ed", "comput-es", "select-ed", "spec-ifies",
            // mnemonic broken across a line in an opcode cell ("AES- ENCWIDE128KL")
            "AES-DECWIDE128KL", "AES-DECWIDE256KL", "AES-ENCWIDE128KL", "AES-ENCWIDE256KL",
        ];

        public static string CleanupHyphenation(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Bullet points to markdown
            text = text.Replace("•\n\n", "\n * ");
            text = text.Replace("•\n", "\n * ");
            text = text.Replace("•", "\n * ");

            // A line break INSIDE a table cell renders a hyphenated word as "func- tions"
            // (hyphen + space) instead of the paragraph form "func-\ntions" (hyphen + newline),
            // because the cell joins its two visual lines with a space. Normalise that hyphen-space
            // to the hyphen-newline form so the curated SplitWords list and the general rule below
            // (which both expect "\n") handle cell text the same as paragraph text. Only a hyphen
            // tight against the preceding letter is a word break (real " - " dashes have a space on
            // BOTH sides and are not matched). The negative lookahead keeps elisions like
            // "16- or 32-bit" / "8- to 64-bit" intact (the continuation is a conjunction, not the
            // rest of the word).
            text = Regex.Replace(text, @"([A-Za-z0-9])- +(?!(?:or|and|to|nor)\b)([A-Za-z])", "$1-\n$2", RegexOptions.None, System.TimeSpan.FromSeconds(2));

            // De-hyphenate the curated single-word splits: "excep-\ntion" -> "exception".
            foreach (string w in SplitWords)
            {
                int h = w.IndexOf('-');
                if (h <= 0) continue;
                string broken = string.Concat(w.AsSpan(0, h), "-\n", w.AsSpan(h + 1));
                string joined = string.Concat(w.AsSpan(0, h), w.AsSpan(h + 1));
                text = text.Replace(broken, joined);
            }

            // Footnote artifact: a trademark glyph (®/™) extracted as its own line in the middle
            // of a hyphenated word break ("Reg-\n®\nisters" -> "Registers"). A lone ®/™ on a line
            // is always noise (a real one is attached, e.g. "Intel®"), so drop the glyph and the
            // hyphen and rejoin the word.
            text = Regex.Replace(text, @"([A-Za-z])-\n[®™]\n([a-z])", "$1$2", RegexOptions.None, System.TimeSpan.FromSeconds(2));

            // General rule for every remaining line-ending hyphen: pull the continuation up onto
            // the same line but KEEP the hyphen. This removes the rendered "foo- bar" space and
            // correctly preserves real compounds the curated list intentionally omits
            // ("floating-\npoint" -> "floating-point", "64-\nbit" -> "64-bit",
            // "general-\nprotection" -> "general-protection"). The lookahead keeps "16-\nor 32-bit"
            // as an elision (renders "16- or 32-bit").
            text = Regex.Replace(text, @"([A-Za-z0-9])-\n(?!(?:or|and|to|nor)\b)([A-Za-z0-9])", "$1-$2", RegexOptions.None, System.TimeSpan.FromSeconds(2));

            // (The old explicit "single- precision" etc. fixes are now covered by the hyphen-space
            // normalisation above plus the general rule, which keeps the hyphen for those compounds.)

            // Edge case: extra space in an instruction alias
            text = text.Replace("REP/REPE/REPZ /REPNE/REPNZ", "REP/REPE/REPZ/REPNE/REPNZ");

            return text;
        }

        /// <summary>
        /// Normalizes text by removing excessive whitespace while preserving structure.
        ///
        /// PDF extraction can introduce:
        /// - Extra spaces between words: "word   word" → "word word"
        /// - Spaces around punctuation: "word , word" → "word, word"
        /// - Multiple newlines: "word\n\n\nword" → "word\n\nword"
        /// </summary>
        public static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Collapse multiple spaces to single space
            text = Regex.Replace(text, @"  +", " ", RegexOptions.None, System.TimeSpan.FromSeconds(2));

            // Collapse multiple newlines to double newline (paragraph break)
            text = Regex.Replace(text, @"\n{3,}", "\n\n", RegexOptions.None, System.TimeSpan.FromSeconds(2));

            return text;
        }

        /// <summary>
        /// Escapes special markdown characters to prevent unintended formatting.
        /// </summary>
        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("#", "\\#").Replace("*", "\\*");
        }
    }
}
