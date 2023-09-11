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

namespace AsmTools
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.Linq;
    using Microsoft.CodeAnalysis.CSharp.Scripting;

    public static class ExpressionEvaluator
    {
        /// <summary> Check if the provided string is a constant. Does not evaluate arithmetic in the string </summary>
        public static (bool valid, ulong value, int nBits) Parse_Constant(string str, bool isCapitals = false)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            string token2;
            bool isHex = false;
            bool isBinary = false;
            bool isDecimal = false;
            bool isOctal = false;
            bool isNegative = false;

            // Console.WriteLine("AsmSourceTools:ToConstant token=" + token);

            str = str.Replace("_", string.Empty);

            if (!isCapitals)
            {
                str = str.ToUpperInvariant();
            }
            str = str.Trim();

            if (str.StartsWith("-", StringComparison.Ordinal))
            {
                token2 = str;
                isDecimal = true;
                isNegative = true;
            }

            // note the special case of token 0h (zero hex) should not be confused with the prefix 0h;
            else if (str.EndsWith("H", StringComparison.Ordinal))
            {
                token2 = str.Substring(0, str.Length - 1);
                isHex = true;
            }
            else if (str.StartsWith("0H", StringComparison.Ordinal) || str.StartsWith("0X", StringComparison.Ordinal) || str.StartsWith("$0", StringComparison.Ordinal))
            {
                token2 = str.Substring(2);
                isHex = true;
            }
            else if (str.StartsWith("0O", StringComparison.Ordinal) || str.StartsWith("0Q", StringComparison.Ordinal))
            {
                token2 = str.Substring(2);
                isOctal = true;
            }
            else if (str.EndsWith("Q", StringComparison.Ordinal) || str.EndsWith("O", StringComparison.Ordinal))
            {
                token2 = str.Substring(0, str.Length - 1);
                isOctal = true;
            }
            else if (str.StartsWith("0D", StringComparison.Ordinal))
            {
                token2 = str.Substring(2);
                isDecimal = true;
            }
            else if (str.EndsWith("D", StringComparison.Ordinal))
            {
                token2 = str;
                isDecimal = true;
            }
            else if (str.StartsWith("0B", StringComparison.Ordinal) || str.StartsWith("0Y", StringComparison.Ordinal))
            {
                token2 = str.Substring(2);
                isBinary = true;
            }
            else if (str.EndsWith("Y", StringComparison.Ordinal))
            {
                token2 = str.Substring(0, str.Length - 1);
                isBinary = true;
            }
            else
            {
                // special case with trailing B: either this B is from a hex number of the Binary
                if (str.EndsWith("B", StringComparison.Ordinal))
                {
                    bool parsedSuccessfully_tmp = ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out ulong dummy);
                    if (parsedSuccessfully_tmp)
                    {
                        isHex = true;
                        token2 = str;
                    }
                    else
                    {
                        token2 = str.Substring(0, str.Length - 1);
                        isBinary = true;
                    }
                }
                else
                { // assume decimal
                    token2 = str;
                    isDecimal = true;
                }
            }
            ulong value = 0;
            bool parsedSuccessfully;
            if (isHex)
            {
                parsedSuccessfully = ulong.TryParse(token2, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value);
            }
            else if (isOctal)
            {
                try
                {
                    value = Convert.ToUInt64(token2, 8);
                    parsedSuccessfully = true;
                }
                catch
                {
                    parsedSuccessfully = false;
                }
            }
            else if (isBinary)
            {
                try
                {
                    value = Convert.ToUInt64(token2, 2);
                    parsedSuccessfully = true;
                }
                catch
                {
                    parsedSuccessfully = false;
                }
            }
            else if (isDecimal)
            {
                if (isNegative)
                {
                    parsedSuccessfully = long.TryParse(token2, NumberStyles.Integer, CultureInfo.CurrentCulture, out long signedValue);
                    value = (ulong)signedValue;
                    // Console.WriteLine("AsmSourceTools:ToConstant token2=" + token2 + "; signed value = " + Convert.ToString(signedValue, 16) + "; unsigned value = " + string.Format(AsmDudeToolsStatic.CultureUI, "{0:X}", value));
                }
                else
                {
                    parsedSuccessfully = ulong.TryParse(token2, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
                    if (!parsedSuccessfully)
                    {
                        parsedSuccessfully = ulong.TryParse(token2, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out value);
                    }
                }
            }
            else
            {
                // unreachable
                parsedSuccessfully = false;
            }

            int nBits = parsedSuccessfully ? AsmSourceTools.NBitsStorageNeeded(value, isNegative) : -1;
            return (valid: parsedSuccessfully, value, nBits);
        }

        public static (bool valid, ulong value, int nBits) Evaluate_Constant(string str, bool isCapitals = false)
        {
            Contract.Requires(str != null);
            Contract.Assume(str != null);

            // 1] test whether str has digits, if it has none it is not a constant
            if (!str.Any(char.IsDigit))
            {
                return (valid: false, value: 0, nBits: -1);
            }

            // 2] test whether str is a constant
            (bool valid, ulong value, int nBits) v = Parse_Constant(str, isCapitals);
            if (v.valid)
            {
                return v;
            }

            // 3] check if str contains operators
            if (str.Contains('+') || str.Contains('-') || str.Contains('*') || str.Contains('/') ||
                str.Contains("<<") || str.Contains(">>"))
            {
                // second: if str is not a constant, test whether evaluating it yields a ulong
                try
                {
                    System.Threading.Tasks.Task<ulong> t = CSharpScript.EvaluateAsync<ulong>(str);
                    ulong value = t.Result;
                    bool isNegative = false;
                    return (valid: true, value, nBits: AsmSourceTools.NBitsStorageNeeded(value, isNegative));
                }
                catch (Exception)
                {
                    // Do nothing
                }
            }
            // 4] don't know what it is but it is not likely to be a constant.
            return (valid: false, value: 0, nBits: -1);
        }
    }
}
