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

using Microsoft.VisualStudio.LanguageServer.Protocol;

using Newtonsoft.Json;

using System.Runtime.Serialization;

namespace AsmDude2LS
{
    //
    // Summary:
    //     Class representing a folding range in a document. See the Language Server Protocol
    //     specification for additional information.
    [DataContract]
    public class AsmFoldingRange
    {
        //
        // Summary:
        //     Gets or sets the start line value.
        [DataMember(Name = "startLine")]
        public int StartLine { get; set; }

        //
        // Summary:
        //     Gets or sets the start character value.
        [DataMember(Name = "startCharacter")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? StartCharacter { get; set; }

        //
        // Summary:
        //     Gets or sets the end line value.
        [DataMember(Name = "endLine")]
        public int EndLine { get; set; }

        //
        // Summary:
        //     Gets or sets the end character value.
        [DataMember(Name = "endCharacter")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? EndCharacter { get; set; }

        //
        // Summary:
        //     Gets or sets the folding range kind.
        [DataMember(Name = "kind")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public FoldingRangeKind? Kind { get; set; }

        /**
             * The text that the client should show when the specified range is
             * collapsed. If not defined or not supported by the client, a default
             * will be chosen by the client.
             *
             * @since 3.17.0 - proposed
             */
        [DataMember(Name = "collapsedText")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string CollapsedText { get; set; }
    }
}
