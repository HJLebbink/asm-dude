using System.Diagnostics.Contracts;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;

namespace intel_doc_2_md
{
    public static class ExtractTextClass
    {
        public static string GetTextPage(PdfDocument? doc, int pageNumber)
        {
            if (doc == null)
            {
                return string.Empty;
            }
            PdfPage? page = doc.Pages[pageNumber];
            CSequence content = ContentReader.ReadContent(page);

            StringBuilder result = new();
            if (false)
            {
                ExtractText(content, result);
                return result.ToString();
            }
            else if (true)
            {
                return ToString(content);
            }
            else
            {
                Collection2D c = new();
                foreach (CObject? element in content)
                {
                    if (element != null)
                    {
                        CType t = GetType(element);
                        Console.WriteLine($"{t}: {element.ToString()}");

                        switch (t)
                        {
                            case CType.CArray:
                                break;
                            case CType.CComment:
                                break;
                            case CType.CInteger:
                                break;
                            case CType.CName:
                                break;
                            case CType.CNumber:
                                break;
                            case CType.COperator:
                                COperator op = (COperator)element;
                                switch (op.OpCode.OpCodeName)
                                {
                                    case OpCodeName.TJ:
                                        var x = op.Operands[0];
                                        break;


                                    default: break;
                                }

                                break;
                            case CType.CReal:
                                break;
                            case CType.CSequence:
                                break;
                            case CType.CString:
                                break;
                            case CType.CUnknown:
                                break;
                            case CType.NULL:
                                break;
                            default:
                                break;
                        }

                    }
                }
                return c.Print(100);
            }
        }


        #region CObject Visitor
        public static void ExtractText(CObject obj, StringBuilder target)
        {
            Contract.Assert(obj != null);

            if (obj is CArray x1) ExtractText(x1, target);
            else if (obj is CComment x2) ExtractText(x2, target);
            else if (obj is CInteger x3)  ExtractText(x3, target);
            else if (obj is CName x4) ExtractText(x4, target);
            else if (obj is CNumber x5) ExtractText(x5, target);
            else if (obj is COperator x6) ExtractText(x6, target);
            else if (obj is CReal x7) ExtractText(x7, target);
            else if (obj is CSequence x8)  ExtractText(x8, target);
            else if (obj is CString x9) ExtractText(x9, target);
            else throw new NotImplementedException(obj.GetType().AssemblyQualifiedName);
        }

        private static void ExtractText(CArray obj, StringBuilder target)
        {
            foreach (var element in obj)
            {
                ExtractText(element, target);
            }
        }
        private static void ExtractText(CComment obj, StringBuilder _) {
            Contract.Assert(obj != null);
            Console.WriteLine($"ExtractText: CComment {obj}");
        }
        private static void ExtractText(CInteger obj, StringBuilder _) {
            Contract.Assert(obj != null);
            Console.WriteLine($"ExtractText: CInteger {obj}");
        }
        private static void ExtractText(CName obj, StringBuilder _)
        {
            Contract.Assert(obj != null);
            Console.WriteLine($"ExtractText: CName {obj}");
        }
        private static void ExtractText(CNumber obj, StringBuilder _)
        {
            Contract.Assert(obj != null);
            //if ((obj._value > 6) || (obj.Value < -1))
            {
                Console.WriteLine($"ExtractText: CNumber {obj}");
            }
        }
        private static void ExtractText(CReal obj, StringBuilder _)
        {
            Contract.Assert(obj != null);
            Console.WriteLine($"ExtractText: CReal {obj}");
        }
        private static void ExtractText(COperator obj, StringBuilder target)
        {
            Contract.Assert(obj != null);
            Console.WriteLine($"ExtractText: COperator {obj}");

            if (obj.OpCode.OpCodeName == OpCodeName.Tj || obj.OpCode.OpCodeName == OpCodeName.TJ)
            {
                foreach (var element in obj.Operands)
                {
                    ExtractText(element, target);
                }
            }
        }
        private static void ExtractText(CSequence obj, StringBuilder target)
        {
            Contract.Assert(obj != null);
            foreach (CObject? element in obj)
            {
                if (element != null)
                {
                    ExtractText(element, target);
                }
            }
        }
        private static void ExtractText(CString obj, StringBuilder target)
        {
            Contract.Assert(obj != null);

            string s = obj.Value;

            s = s.Replace('\u0097', '-');

            Console.WriteLine($"ExtractText: CString {s}");
            target.Append(s);
        }
        #endregion


        private static string Tabs(int depth)
        {
            string result = "";
            for (int i = 0; i<depth; ++i)
            {
                result += "  ";
            }
            return result;
        }


        public enum CType
        {
            CArray, CComment, CInteger, CName, CNumber, COperator, CReal, CSequence, CString, CUnknown, NULL
        }

        public static CType GetType(CObject? obj)
        {
            if (obj == null) return CType.NULL;
            else if (obj is CArray) return CType.CArray;
            else if (obj is CSequence) return CType.CSequence;
            else if (obj is CComment) return CType.CComment;
            else if (obj is CInteger) return CType.CInteger;
            else if (obj is CReal) return CType.CReal;
            else if (obj is CName) return CType.CName;
            else if (obj is CNumber) return CType.CNumber;
            else if (obj is COperator) return CType.COperator;
            else if (obj is CString) return CType.CString;
            return CType.CUnknown;
        }

        public static float GetFloatValue(CObject? obj)
        {
            if (obj == null) return float.NaN;
            else if (obj is CInteger x1) return (float)x1.Value;
            else if (obj is CReal x2) return (float)x2.Value;
            else if (obj is CNumber) return float.Parse(obj.ToString(), NumberStyles.Any);
            return float.NaN;
        }

        public static string ToString(CObject obj, int depth = 0) {
            return GetType(obj) switch
            {
                CType.CArray => ToString(obj as CArray, depth + 1),
                CType.CComment => ToString(obj as CComment, depth + 1),
                CType.CInteger => ToString(obj as CInteger, depth + 1),
                CType.CName => ToString(obj as CName, depth + 1),
                CType.CNumber => ToString(obj as CNumber, depth + 1),
                CType.COperator => ToString(obj as COperator, depth + 1),
                CType.CReal => ToString(obj as CReal, depth + 1),
                CType.CSequence => ToString(obj as CSequence, depth + 1),
                CType.CString => ToString(obj as CString, depth + 1),
                CType.CUnknown => "Unknown\n",
                CType.NULL => "NULL\n",
                _ => "NOT implemented\n",
            };
        }

        public static string ToString(CArray obj, int depth = 0) 
        {
            string result = "";
            for (int i = 0; i < obj.Count; ++i)
            {                
                result += ToString(obj[i], depth + 1);
            }
            return result;
        }
        public static string ToString(CComment obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            return $"{Tabs(depth)} CComment {obj}\n";
        }
        public static string ToString(CInteger obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            return $"{Tabs(depth)} CInteger {obj}\n";
        }
        public static string ToString(CName obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            return $"{Tabs(depth)} CName {obj}\n";
        }
        public static string ToString(CNumber obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            return $"{Tabs(depth)} CNumber {obj}\n";
        }
        public static string ToString(CReal obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            return $"{Tabs(depth)} CReal {obj}\n";
        }
        public static string ToString(COperator obj, int depth = 0)
        {
            Contract.Assert(obj != null);

            string result2 = $"{Tabs(depth)}COperator {obj.OpCode.Name}\n";
            foreach (var element in obj.Operands)
            {
                result2 += ToString(element, depth + 1);
            }
            return result2;


            return result2;

            switch (obj.OpCode.OpCodeName)
            {
                case OpCodeName.Tc: // set character spacing: ignore
                case OpCodeName.Tw: // set word spacing: ignore
                case OpCodeName.g: // set stroking color: ignore
                case OpCodeName.rg: // set RGB color: ignore
                case OpCodeName.Tf: // set text font and size
                case OpCodeName.BT: // begin text object
                case OpCodeName.ET: // end text object
                    return "";
                case OpCodeName.f:
                    return "";

                case OpCodeName.TD:
                    {
                        float xD = Math.Abs(GetFloatValue(obj.Operands[0]));
                        float yD = Math.Abs(GetFloatValue(obj.Operands[1]));
                        
                        if (yD < 0.1)
                        {
                            return $"[concat {xD};{yD}]";
                        } else
                        {
                            return $"[newline {xD};{yD}]";
                        }                        
                        //return $"{Tabs(depth)} COperator {obj} ({xD};{yD})\n";
                    }
//                case OpCodeName.Tm:
                    

                case OpCodeName.TJ:
                    {
                        return $"{Tabs(depth)} COperator {obj} {ToStringTJ(obj.Operands, depth + 1)}";
                    }
                case OpCodeName.Tj:
                    {
                        string result = $"{Tabs(depth)} COperator {obj}\n";
                        foreach (var element in obj.Operands)
                        {
                            result += ToString(element, depth + 1);
                        }
                        return result;
                    }
                default:
                    {
                        string result = $"{Tabs(depth)} COperator {obj}\n";
                        foreach (var element in obj.Operands)
                        {
                            result += ToString(element, depth + 1);
                        }
                        return result;
                    }
            }
        }

        public static string ToStringTJ(CSequence obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            string result = $"{Tabs(depth)}\"";

            foreach (CObject? element in obj)
            {
                var t = GetType(element);
                switch (t)
                {
                    case CType.NULL: result += "[NULL]"; break;
                    case CType.CString:
                        {
                            string str = ((CString)element).Value;
                            result += str;
                            break;
                        }
                    case CType.CInteger:
                    case CType.CReal:
                    case CType.CNumber:
                        {
                            float f = GetFloatValue(element);
                            if (Math.Abs(f) > 150)
                            {
                                result += $" [tab {f}] ";
                            }
                            break;
                        }
                    case CType.CArray:
                        {
                            result += ToStringTJ((CArray)element, depth + 1); break;
                        }
                    default: result += "[ERROR " + t.ToString() +"]"; break;
                }
            }
            return result +"\"\n";
        }


        public static string ToString(CSequence obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            string result = $"{Tabs(depth)} CSequence (with {obj.Count} elements)\n";

            foreach (CObject? element in obj)
            {
                if (element != null)
                {
                    result += ToString(element, depth + 1);
                }
            }
            return result;
        }
        public static string ToString(CString obj, int depth = 0)
        {
            Contract.Assert(obj != null);
            string s = obj.Value;
            s = s.Replace('\u0097', '-');
            return $"{Tabs(depth)} CString {s}\n";
        }
    }
}