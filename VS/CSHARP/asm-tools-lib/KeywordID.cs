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

namespace AsmTools
{
    public readonly struct KeywordID
    {
        private readonly ulong data;

        public KeywordID(int lineNumber, int fileID, int startPos, int endPos, AsmTokenType type = AsmTokenType.UNKNOWN)
        {
            this.data = ((ulong)lineNumber & 0xFFFFFF) // 24 bits for linenumber
                | ((ulong)(fileID & 0xFF) << 24)  // 8 bits for fileID
                | ((ulong)(startPos & 0x3FFF) << 32)  // 14 bits for startPos
                | ((ulong)(endPos & 0x3FFF) << 46)  // 14 bits for endPos
                | ((ulong)(type) << 60); // 4 bits for type
        }

        public int LineNumber
        {
            get
            {
                return (int)(this.data & 0x00FFFFFF);
            }
        }

        public int File_Id
        {
            get
            {
                return (int)((this.data >> 24) & 0xFF);
            }
        }

        public int Start_Pos
        {
            get
            {
                return (int)((this.data >> 32) & 0x3FFF);
            }

        }

        public int End_Pos
        {
            get
            {
                return (int)((this.data >> 46) & 0x3FFF);
            }
        }

        public AsmTokenType Type
        {
            get
            {
                return (AsmTokenType)((this.data >> 60) & 0xF);
            }
        }

        public bool Is_From_Main_File
        {
            get
            {
                return this.File_Id == 0;
            }
        }

        public override string ToString()
        {
            return $"KeywordID({this.LineNumber}, {this.File_Id}, {this.Start_Pos}, {this.End_Pos}, {this.Type})";
        }
    }
}
