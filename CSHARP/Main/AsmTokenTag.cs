// The MIT License (MIT)
//
// Copyright (c) 2016 Henk-Jan Lebbink
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

namespace AsmDude {
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.IO;
    using System.Globalization;

    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Classification;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Utilities;


    [Export(typeof(ITaggerProvider))]
    [ContentType("asm!")]
    [TagType(typeof(AsmTokenTag))]
    internal sealed class AsmTokenTagProvider : ITaggerProvider {

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            return new AsmTokenTagger(buffer) as ITagger<T>;
        }
    }

    public class AsmTokenTag : ITag {
        public AsmTokenTypes type { get; private set; }

        public AsmTokenTag(AsmTokenTypes type) {
            this.type = type;
        }
    }

    internal sealed class AsmTokenTagger : ITagger<AsmTokenTag> {

        ITextBuffer _buffer;
        IDictionary<string, AsmTokenTypes> _asmTypes;

        static char[] splitChars = { ' ', ',', '\t', '+', '*', '[', ']' };

        internal AsmTokenTagger(ITextBuffer buffer) {
            _buffer = buffer;
            _asmTypes = new Dictionary<string, AsmTokenTypes>();

            // fill the dictionary with keywords

            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filenameData = "AsmDudeData.xml";
            string filenameDll = "AsmDude.dll";
            string filename = fullPath.Substring(0, fullPath.Length - filenameDll.Length) + filenameData;
            Debug.WriteLine("INFO: AsmTokenTagger: going to load file \"" + filename + "\"");
            XmlDocument xmlDoc = new XmlDocument();
            try {
                xmlDoc.Load(filename);

                foreach (XmlNode node in xmlDoc.SelectNodes("//misc"))
                {
                    var nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null) {
                        Debug.WriteLine("WARNING: AsmTokenTagger: found misc with no name");
                    }
                    else {
                        string name = nameAttribute.Value.ToUpper();
                        //Debug.WriteLine("INFO: AsmTokenTagger: found misc " + name);
                        _asmTypes[name] = AsmTokenTypes.Misc;
                    }
                }

                foreach (XmlNode node in xmlDoc.SelectNodes("//directive")) {
                    var nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null) {
                        Debug.WriteLine("WARNING: AsmTokenTagger: found directive with no name");
                    } else {
                        string name = nameAttribute.Value.ToUpper();
                        //Debug.WriteLine("INFO: AsmTokenTagger: found directive " + name);
                        _asmTypes[name] = AsmTokenTypes.Directive;
                    }
                }
                foreach (XmlNode node in xmlDoc.SelectNodes("//mnemonic")) {
                    var nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null) {
                        Debug.WriteLine("WARNING: AsmTokenTagger: found mnemonic with no name");
                    } else {
                        string name = nameAttribute.Value.ToUpper();
                        //Debug.WriteLine("INFO: AsmTokenTagger: found mnemonic " + name);

                        var typeAttribute = node.Attributes["type"];
                        if (typeAttribute == null) {
                            _asmTypes[name] = AsmTokenTypes.Mnemonic;
                        } else {
                            if (typeAttribute.Value.ToUpper().Equals("JUMP")) {
                                _asmTypes[name] = AsmTokenTypes.Jump;
                            } else {
                                _asmTypes[name] = AsmTokenTypes.Mnemonic;
                            }
                        }
                    }
                }
                foreach (XmlNode node in xmlDoc.SelectNodes("//register")) {
                    var nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null) {
                        Debug.WriteLine("WARNING: AsmTokenTagger: found register with no name");
                    } else {
                        string name = nameAttribute.Value.ToUpper();
                        //Debug.WriteLine("INFO: AsmTokenTagger: found register " + name);
                        _asmTypes[name] = AsmTokenTypes.Register;
                    }
                }

            }
            catch (FileNotFoundException ex1) {
                Debug.WriteLine("ERROR: AsmTokenTagger: could not find file \"" + filename + "\". " + ex1);
            } catch (XmlException ex2) {
                Debug.WriteLine("ERROR: AsmTokenTagger: error while reading find \"" + filename + "\". " + ex2);
            } finally {
                xmlDoc = null; // housekeeping, xmlDoc can be garbage collected
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged {
            add { }
            remove { }
        }

        public IEnumerable<ITagSpan<AsmTokenTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            foreach (SnapshotSpan curSpan in spans) {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                string[] tokens = containingLine.GetText().ToLower().Split(splitChars);

                int curLoc = containingLine.Start.Position;
                int nextLoc = curLoc;

                bool foundRemark = false;
                int tokenId = 0;

                while ((tokenId < tokens.Length) && !foundRemark) {

                    var tup = getNextToken(tokenId, nextLoc, tokens);
                    tokenId = tup.Item2;
                    nextLoc = tup.Item3;

                    if (tup.Item1) {
                        string asmToken = tup.Item4;
                        curLoc = nextLoc - (asmToken.Length + 1);

                        //Debug.WriteLine("token "+tokenId+" at location "+curLoc+" = \"" + asmToken + "\"");

                        if (containsRemarkSymbol(asmToken)) {
                            foundRemark = true;
                        } else {
                            if (this._asmTypes.ContainsKey(asmToken)) {
                                switch (this._asmTypes[asmToken]) {

                                    case AsmTokenTypes.Jump: {
                                            //Debug.WriteLine("current jump token \"" + asmToken + "\"");

                                            var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenTypes.Jump));

                                            tup = getNextToken(tokenId, nextLoc, tokens);
                                            tokenId = tup.Item2;
                                            nextLoc = tup.Item3;

                                            if (tup.Item1) {
                                                asmToken = tup.Item4;
                                                curLoc = nextLoc - (asmToken.Length + 1);
                                                //Debug.WriteLine("label token " + tokenId + " at location " + curLoc + " = \"" + asmToken + "\"");

                                                if (containsRemarkSymbol(asmToken)) {
                                                    foundRemark = true;
                                                } else {
                                                    tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                                    if (tokenSpan.IntersectsWith(curSpan))
                                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenTypes.Label));
                                                }
                                            }
                                            break;
                                        }
                                    default: {
                                            var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                            if (tokenSpan.IntersectsWith(curSpan))
                                                yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(_asmTypes[asmToken]));
                                            break;
                                        }
                                }
                            } else { // asmToken is not a known keyword, check if it is an numerical
                                if (isConstant(asmToken)) {
                                    var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, asmToken.Length));
                                    if (tokenSpan.IntersectsWith(curSpan))
                                        yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenTypes.Constant));
                                    break;
                                }
                            }
                        }
                    }

                    if (foundRemark) {
                        //Debug.WriteLine("found remark: curLoc=" + curLoc);

                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, containingLine.End.Position - curLoc));
                        if (tokenSpan.IntersectsWith(curSpan)) {
                            yield return new TagSpan<AsmTokenTag>(tokenSpan, new AsmTokenTag(AsmTokenTypes.Remark));
                        }
                    }
                }
            }
        }

        private bool containsRemarkSymbol(string token) {
            return (token.Contains("#") || token.Contains(";"));
        }

        // return true, nextTokenId, tokenEndPos, tokenString
        private Tuple<bool, int, int, string> getNextToken(int tokenId, int startLoc, string[] tokens) {
            int nextTokenId = tokenId;
            int nextLoc = startLoc;

            while (nextTokenId < tokens.Length) {
                string asmToken = tokens[nextTokenId];
                nextTokenId++;
                //Debug.WriteLine("getNextToken:nextTokenId=" + nextTokenId+ "; asmToken=\""+asmToken+"\"");
                if (asmToken.Length > 0) {
                    nextLoc += asmToken.Length + 1; //add an extra char location because of the separator
                    return new Tuple<bool, int, int, string>(true, nextTokenId, nextLoc, asmToken.ToUpper());
                } else {
                    nextLoc++;
                }
            }
            return new Tuple<bool, int, int, string>(false, nextTokenId, nextLoc, "");
        }

        private bool isConstant(string token) {
            string token2;
            if (token.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)) {
                token2 = token.Substring(2);
            } else {
                token2 = token;
            }
            ulong dummy;
            bool parsedSuccessfully = ulong.TryParse(token2, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out dummy);
            return parsedSuccessfully;
        }
    }
}
