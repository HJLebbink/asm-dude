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

namespace AsmDude3.SyntaxHighlighting
{
    using Microsoft.VisualStudio.Text.Tagging;
    using AsmTools;

    /// <summary>
    /// Represents a single token in assembly code with its type and optional metadata.
    /// Used by taggers to provide token information for syntax highlighting and analysis.
    /// </summary>
    public class AsmTokenTag : ITag
    {
        /// <summary>
        /// Keyword token metadata: "PROTO" indicates a procedure prototype
        /// </summary>
        public static readonly string MISC_KEYWORD_PROTO = "PROTO";

        /// <summary>
        /// Gets the type of this token
        /// </summary>
        public AsmTokenType Type { get; }

        /// <summary>
        /// Gets optional miscellaneous data associated with this token
        /// Examples: "PROTO" for procedure prototypes
        /// </summary>
        public string Misc { get; }

        /// <summary>
        /// Initializes a new token tag with just a type
        /// </summary>
        /// <param name="type">The token type</param>
        public AsmTokenTag(AsmTokenType type)
            : this(type, null)
        {
        }

        /// <summary>
        /// Initializes a new token tag with type and optional metadata
        /// </summary>
        /// <param name="type">The token type</param>
        /// <param name="misc">Optional metadata about the token</param>
        public AsmTokenTag(AsmTokenType type, string misc)
        {
            this.Type = type;
            this.Misc = misc;
        }
    }
}
