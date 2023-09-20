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

namespace AsmDude2LS
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    //using System.Windows.Media;
    //using System.Windows.Media.Imaging;
    //using System.Windows.Media.Imaging;
    //using AsmDude2.SyntaxHighlighting;
    using AsmTools;
    //using EnvDTE;
    //using Microsoft.VisualStudio;
    //using Microsoft.VisualStudio.Shell;
    //using Microsoft.VisualStudio.Shell.Interop;
    //using Microsoft.VisualStudio.Text;
    //using Microsoft.VisualStudio.Text.Tagging;
    //using Microsoft.VisualStudio.TextManager.Interop;
    //using Microsoft.VisualStudio.Utilities;

    public static class AsmDudeToolsStatic
    {
        public static readonly CultureInfo CultureUI = CultureInfo.CurrentUICulture;


        #region Singleton Factories

        //public static void Print_Speed_Warning(DateTime startTime, string component)
        //{
        //    double elapsedSec = (double)(DateTime.Now.Ticks - startTime.Ticks) / 10000000;
        //    if (elapsedSec > .SlowWarningThresholdSec)
        //    {
        //        Output_WARNING(string.Format(CultureUI, "SLOW: took {0} {1:F3} seconds to finish", component, elapsedSec));
        //    }
        //}

        #endregion Singleton Factories

        /// <summary>Guess whether the provided buffer has assembly in Intel syntax (return true) or AT&T syntax (return false)</summary>
        //[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
        //public static bool Guess_Intel_Syntax(ITextBuffer buffer, int nLinesMax = 30)
        //{
        //    Contract.Requires(buffer != null);

        //    bool contains_register_att(List<string> line)
        //    {
        //        foreach (string asmToken in line)
        //        {
        //            if (asmToken[0].Equals('%'))
        //            {
        //                string asmToken2 = asmToken.Substring(1);
        //                if (RegisterTools.IsRn(asmToken2, true))
        //                {
        //                    return true;
        //                }
        //            }
        //        }
        //        return false;
        //    }
        //    bool contains_register_intel(List<string> line)
        //    {
        //        foreach (string asmToken in line)
        //        {
        //            if (RegisterTools.IsRn(asmToken, true))
        //            {
        //                return true;
        //            }
        //        }
        //        return false;
        //    }
        //    bool contains_constant_att(List<string> line)
        //    {
        //        foreach (string asmToken in line)
        //        {
        //            if (asmToken[0].Equals('$'))
        //            {
        //                return true;
        //            }
        //        }
        //        return false;
        //    }
        //    bool contains_constant_intel(List<string> line)
        //    {
        //        return false;
        //    }
        //    bool contains_mnemonic_att(List<string> line)
        //    {
        //        foreach (string word in line)
        //        {
        //            if (!AsmSourceTools.IsMnemonic(word, true))
        //            {
        //                if (AsmSourceTools.IsMnemonic_Att(word, true))
        //                {
        //                    return true;
        //                }
        //            }
        //        }
        //        return false;
        //    }
        //    bool contains_mnemonic_intel(List<string> line)
        //    {
        //        return false;
        //    }

        //    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Intel_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
        //    ITextSnapshot snapshot = buffer.CurrentSnapshot;
        //    int registers_i = 0;
        //    int constants_i = 0;
        //    int mnemonics_i = 0;

        //    for (int i = 0; i < Math.Min(snapshot.LineCount, nLinesMax); ++i)
        //    {
        //        string line_upcase = snapshot.GetLineFromLineNumber(i).GetText().ToUpperInvariant();
        //        Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_upcase));

        //        List<string> keywords_upcase = AsmSourceTools.SplitIntoKeywordsList(line_upcase);

        //        if (contains_register_att(keywords_upcase))
        //        {
        //            registers_i++;
        //        }

        //        if (contains_register_intel(keywords_upcase))
        //        {
        //            registers_i--;
        //        }

        //        if (contains_constant_att(keywords_upcase))
        //        {
        //            constants_i++;
        //        }

        //        if (contains_constant_intel(keywords_upcase))
        //        {
        //            constants_i--;
        //        }

        //        if (contains_mnemonic_att(keywords_upcase))
        //        {
        //            mnemonics_i++;
        //        }

        //        if (contains_mnemonic_intel(keywords_upcase))
        //        {
        //            mnemonics_i--;
        //        }
        //    }
        //    int total =
        //        Math.Max(Math.Min(1, registers_i), -1) +
        //        Math.Max(Math.Min(1, constants_i), -1) +
        //        Math.Max(Math.Min(1, mnemonics_i), -1);

        //    bool result = (total <= 0);
        //    Output_INFO(string.Format(CultureUI, "{0}:Guess_Intel_Syntax; result {1}; file=\"{2}\"; registers {3}; constants {4}; mnemonics {5}", "AsmDudeToolsStatic", result, GetFilename(buffer), registers_i, constants_i, mnemonics_i));
        //    return result;
        //}

        ///// <summary>Guess whether the provided buffer has assembly in Masm syntax (return true) or Gas syntax (return false)</summary>
        //public static bool Guess_Masm_Syntax(ITextBuffer buffer, int nLinesMax = 30)
        //{
        //    Contract.Requires(buffer != null);

        //    //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax. file=\"{1}\"", "AsmDudeToolsStatic", AsmDudeToolsStatic.GetFilename(buffer)));
        //    ITextSnapshot snapshot = buffer.CurrentSnapshot;
        //    int evidence_masm = 0;

        //    for (int i = 0; i < Math.Min(snapshot.LineCount, nLinesMax); ++i)
        //    {
        //        string line_upcase = snapshot.GetLineFromLineNumber(i).GetText().ToUpperInvariant();
        //        //AsmDudeToolsStatic.Output_INFO(string.Format(AsmDudeToolsStatic.CultureUI, "{0}:Guess_Masm_Syntax {1}:\"{2}\"", "AsmDudeToolsStatic", i, line_capitals));

        //        List<string> keywords_upcase = AsmSourceTools.SplitIntoKeywordsList(line_upcase);

        //        foreach (string keyword_upcase in keywords_upcase)
        //        {
        //            switch (keyword_upcase)
        //            {
        //                case "PTR":
        //                case "@B":
        //                case "@F":
        //                    evidence_masm++;
        //                    break;
        //                case ".INTEL_SYNTAX":
        //                case ".ATT_SYNTAX":
        //                    return false; // we know for sure
        //            }
        //        }
        //    }
        //    bool result = (evidence_masm > 0);
        //    Output_INFO(string.Format(CultureUI, "{0}:Guess_Masm_Syntax; result {1}; file=\"{2}\"; evidence_masm {3}", "AsmDudeToolsStatic", result, GetFilename(buffer), evidence_masm));
        //    return result;
        //}

        /// <summary>
        /// Get the path where this visual studio extension is installed.
        /// </summary>
        public static string Get_Install_Path()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string Cleanup(string line)
        {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > LanguageServer.MaxNumberOfCharsInToolTips)
            {
                return cleanedString.Substring(0, LanguageServer.MaxNumberOfCharsInToolTips - 3) + "...";
            }
            else
            {
                return cleanedString;
            }
        }

        /// <summary>
        /// Find the previous keyword (if any) that exists BEFORE the provided triggerPoint, and the provided start.
        /// Eg. qqqq xxxxxx yyyyyyy zzzzzz
        ///     ^             ^
        ///     |begin        |end
        /// the previous keyword is xxxxxx
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        //public static string Get_Previous_Keyword(SnapshotPoint begin, SnapshotPoint end)
        //{
        //    // return getPreviousKeyword(begin.GetContainingLine.)
        //    if (end == 0)
        //    {
        //        return string.Empty;
        //    }

        //    int beginLine = begin.GetContainingLine().Start;
        //    int beginPos = begin.Position - beginLine;
        //    int endPos = end.Position - beginLine;
        //    return AsmSourceTools.GetPreviousKeyword(beginPos, endPos, begin.GetContainingLine().GetText());
        //}

        public static bool Is_All_upcase(string input)
        {
            Contract.Requires(input != null);

            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetter(input[i]) && !char.IsUpper(input[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static string Make_Full_Qualified_Label(string prefix, string label2, AssemblerEnum assembler)
        {
            if (assembler.HasFlag(AssemblerEnum.MASM))
            {
                if ((prefix != null) && (prefix.Length > 0))
                {
                    return "[" + prefix + "]" + label2;
                }
                else
                {
                    return label2;
                }
            }
            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL))
            {
                if ((prefix != null) && (prefix.Length > 0))
                {
                    return prefix + label2;
                }
                else
                {
                    return label2;
                }
            }
            return prefix + label2;
        }

        public static string Retrieve_Regular_Label(string label, AssemblerEnum assembler)
        {
            Contract.Requires(label != null);

            if (assembler.HasFlag(AssemblerEnum.MASM))
            {
                if ((label.Length > 0) && label[0].Equals('['))
                {
                    for (int i = 1; i < label.Length; ++i)
                    {
                        char c = label[i];
                        if (c.Equals(']'))
                        {
                            return label.Substring(i + 1);
                        }
                    }
                }
            }
            else if (assembler.HasFlag(AssemblerEnum.NASM_INTEL))
            {
                for (int i = 0; i < label.Length; ++i)
                {
                    char c = label[i];
                    if (c.Equals('.'))
                    {
                        return label.Substring(i);
                    }
                }
            }
            return label;
        }
    }
}
