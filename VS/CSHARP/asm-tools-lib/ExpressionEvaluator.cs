// The MIT License (MIT)
//
// Copyright (c) 2017 Henk-Jan Lebbink
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
using System.Globalization;

namespace AsmTools
{
    public class ExpressionEvaluator
    {
        public enum Op
        {
            NONE,
            PLUS,
            MINUS,
            TIMES,
            DIV,
            OR,
            AND,
            SHL,
            SHR,
            CLOSING,
            OPENING
        }

        /// <summary> Check if the provided string is a constant. Does not evaluate arithmetic in the string </summary>
        public static (bool Valid, ulong Value, int NBits) Parse_Constant(string str, bool isCapitals=false)
        {
            string token2;
            bool isHex = false;
            bool isBinary = false;
            bool isDecimal = false;
            bool isOctal = false;
            bool isNegative = false;

            //Console.WriteLine("AsmSourceTools:ToConstant token=" + token);

            if (!isCapitals)
            {
                str = str.ToUpper();
            }
            str = str.Trim();

            if (str.StartsWith("-"))
            {
                token2 = str;
                isDecimal = true;
                isNegative = true;
            }


            // note the special case of token 0h (zero hex) should not be confused with the prefix 0h;
            else if (str.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(0, str.Length - 1);
                isHex = true;
            }
            else if (str.StartsWith("0H", StringComparison.OrdinalIgnoreCase) || str.StartsWith("0X", StringComparison.OrdinalIgnoreCase) || str.StartsWith("$0"))
            {
                token2 = str.Substring(2);
                isHex = true;
            }
            else if (str.StartsWith("0B", StringComparison.OrdinalIgnoreCase) || str.StartsWith("0Y", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(2);
                isBinary = true;
            }
            else if (str.EndsWith("B", StringComparison.OrdinalIgnoreCase) || str.EndsWith("Y", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(0, str.Length - 1);
                isBinary = true;
            }
            else if (str.StartsWith("0O", StringComparison.OrdinalIgnoreCase) || str.StartsWith("0Q", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(2);
                isOctal = true;
            }
            else if (str.EndsWith("Q", StringComparison.OrdinalIgnoreCase) || str.EndsWith("O", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(0, str.Length - 1);
                isOctal = true;
            }
            else if (str.StartsWith("0d", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str.Substring(2);
                isDecimal = true;
            }
            else if (str.EndsWith("D", StringComparison.OrdinalIgnoreCase))
            {
                token2 = str;
                isDecimal = true;
            }
            else
            {   // assume decimal
                token2 = str;
                isDecimal = true;
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
                    //Console.WriteLine("AsmSourceTools:ToConstant token2=" + token2 + "; signed value = " + Convert.ToString(signedValue, 16) + "; unsigned value = " + string.Format("{0:X}", value));
                }
                else
                {
                    parsedSuccessfully = ulong.TryParse(token2, NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
                }
            }
            else
            {
                // unreachable
                parsedSuccessfully = false;
            }

            int nBits = (parsedSuccessfully) ? AsmSourceTools.NBitsStorageNeeded(value, isNegative) : -1;
            return (Valid: parsedSuccessfully, Value: value, NBits: nBits);
        }

        public static (bool Valid, ulong Value, int NBits) Evaluate_Constant(string str, bool IsCapitals = false)
        {
            //Console.WriteLine("INFO: Evaluate_Constant: str=" + str + ": length=" + str.Length);

            if (!IsCapitals) str = str.ToUpper();

            Stack<ulong> vStack = new Stack<ulong>();
            Stack<Op> opStack = new Stack<Op>();

            opStack.Push(Op.OPENING); // Implicit opening parenthesis

            int pos = 0;
            int exprLength = str.Length;

            while (pos <= exprLength)
            {
                if (pos == exprLength)
                {
                    Process_Closing_Parenthesis(vStack, opStack);
                    pos++;
                }
                else
                {
                    char c = str[pos];
                    if (Char.IsWhiteSpace(c))
                    {
                        pos++;
                    }
                    else
                    {
                        switch (c)
                        {
                            case ')':
                                Process_Closing_Parenthesis(vStack, opStack);
                                pos++;
                                break;
                            case '(':
                                Process_Opening_Parenthesis(vStack, opStack);
                                pos++;
                                break;
                            case '+':
                                if (!Process_Input_Operator(Op.PLUS, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '-':
                                if (!Process_Input_Operator(Op.MINUS, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '*':
                                if (!Process_Input_Operator(Op.TIMES, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '/':
                                if (!Process_Input_Operator(Op.DIV, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '|':
                                if (!Process_Input_Operator(Op.OR, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '&':
                                if (!Process_Input_Operator(Op.AND, vStack, opStack)) return (false, 0, -1);
                                pos++;
                                break;
                            case '<':
                                if ((pos + 1 < exprLength) && (str[pos + 1] == '<'))
                                {
                                    pos++;
                                    if (!Process_Input_Operator(Op.SHL, vStack, opStack)) return (false, 0, -1);
                                    pos++;
                                }
                                break;
                            case '>':
                                if ((pos + 1 < exprLength) && (str[pos + 1] == '>'))
                                {
                                    pos++;
                                    if (!Process_Input_Operator(Op.SHR, vStack, opStack)) return (false, 0, -1);
                                    pos++;
                                }
                                break;
                            default:
                                var v = Process_Input_Number(str, pos, vStack);
                                if (!v.Valid) return (false, 0, -1);
                                pos = v.Pos;
                                break;
                        }

                    }
                }
            }

            if (vStack.Count == 1) // Result remains on values stacks
            {
                ulong v = vStack.Pop();
                return (true, v, AsmSourceTools.NBitsStorageNeeded(v, false));
            }
            else
            {
                return (false, 0, -1);
            }
        }

        private static void Process_Closing_Parenthesis(Stack<ulong> vStack, Stack<Op> opStack)
        {
            while (opStack.Peek() != Op.OPENING)
            {
                Execute_Operation(vStack, opStack);
            }
            opStack.Pop(); // Remove the opening parenthesis
        }

        private static void Process_Opening_Parenthesis(Stack<ulong> vStack, Stack<Op> opStack)
        {
            opStack.Push(Op.OPENING);
        }

        private static (bool Valid, int Pos) Process_Input_Number(string exp, int pos, Stack<ulong> vStack)
        {
            int beginPos = pos;
            bool proceed = pos < exp.Length;
            while (proceed)
            {
                char c = exp[pos];
                if (Char.IsWhiteSpace(c))
                {
                    proceed = false;
                }
                else
                {
                    switch (c)
                    {
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case 'A':
                        case 'B':
                        case 'C':
                        case 'D':
                        case 'E':
                        case 'F':
                        case 'X':
                        case 'H':
                            pos++;
                            proceed = pos < exp.Length;
                            break;
                        case '+':
                        case '-':
                        case '*':
                        case '/':
                        case '|':
                        case '&':
                        case '>':
                        case '<':
                        case '(':
                        case ')':
                            proceed = false;
                            break;
                        default:
                            return (false, 0); // found invalid char
                    }
                }
            }

            int endPos = pos;
            if (endPos > beginPos)
            {
                int length = (endPos - beginPos);
                var v = Parse_Constant(exp.Substring(beginPos, length));
                if (v.Valid)
                {
                    vStack.Push(v.Value);
                }
            }
            return (true, pos);
        }

        private static bool Process_Input_Operator(Op op, Stack<ulong> vStack, Stack<Op> opStack)
        {
            while ((opStack.Count > 0) && Operator_Causes_Evaluation(op, opStack.Peek()))
            {
                if (!Execute_Operation(vStack, opStack))
                {
                    return false;
                }
            }
            opStack.Push(op);
            return true;
        }

        private static bool Operator_Causes_Evaluation(Op op, Op prevOp)
        {
            bool evaluate = false;
            switch (op)
            {
                case Op.PLUS:
                case Op.MINUS:
                case Op.SHL:
                case Op.SHR:
                case Op.OR:
                case Op.AND:
                    evaluate = (prevOp != Op.OPENING);
                    break;
                case Op.TIMES:
                case Op.DIV:
                    evaluate = ((prevOp == Op.TIMES) || (prevOp == Op.DIV));
                    break;
                case Op.CLOSING:
                    evaluate = true;
                    break;
            }
            return evaluate;
        }

        private static bool Execute_Operation(Stack<ulong> vStack, Stack<Op> opStack)
        {
            if (vStack.Count > 1)
            {
                ulong rightOperand = vStack.Pop();
                ulong leftOperand = vStack.Pop();
                ulong result = 0;
                switch (opStack.Pop())
                {
                    case Op.PLUS:
                        result = leftOperand + rightOperand;
                        break;
                    case Op.MINUS:
                        result = leftOperand - rightOperand;
                        break;
                    case Op.TIMES:
                        result = leftOperand * rightOperand;
                        break;
                    case Op.DIV:
                        result = leftOperand / rightOperand;
                        break;
                    case Op.OR:
                        result = leftOperand | rightOperand;
                        break;
                    case Op.AND:
                        result = leftOperand & rightOperand;
                        break;
                    case Op.SHL:
                        result = leftOperand << (int)(0xFF & rightOperand);
                        break;
                    case Op.SHR:
                        result = leftOperand >> (int)(0xFF & rightOperand);
                        break;
                    default:
                        break;
                }
                vStack.Push(result);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
