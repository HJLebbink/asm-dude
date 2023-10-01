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
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    public sealed class AsmDude2Tools : IDisposable
    {
        private readonly TraceSource traceSource;
        private readonly XmlDocument xmlData_;
        private readonly Dictionary<string, AsmTokenType> type_;
        private readonly Dictionary<string, AssemblerEnum> assembler_;
        private readonly Dictionary<string, Arch> arch_;
        private readonly Dictionary<string, string> description_;

        public static AsmDude2Tools Create(string path, TraceSource traceSource)
        {
            if (Instance == null)
            {
                Instance = new AsmDude2Tools(traceSource);
                Instance.Init_Data(path);
            }
            return Instance;
        }

        private static AsmDude2Tools Instance
        {
            get; 
            set; 
        }

        /// <summary>
        /// Singleton pattern: use AsmDudeTools.Instance for the instance of this class
        /// </summary>
        private AsmDude2Tools(TraceSource traceSource)
        {
            this.traceSource = traceSource;
            this.xmlData_ = new XmlDocument() { XmlResolver = null };
            this.type_ = new Dictionary<string, AsmTokenType>();
            this.arch_ = new Dictionary<string, Arch>();
            this.assembler_ = new Dictionary<string, AssemblerEnum>();
            this.description_ = new Dictionary<string, string>();
        }

        #region Public Methods

        public AsmTokenType Get_Token_Type_Att(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Assume(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            int length = keyword.Length;
            Contract.Requires(length > 0);

            char firstChar = keyword[0];

            #region Test if keyword is a register
            if (firstChar == '%')
            {
                string keyword2 = keyword.Substring(1);
                Rn reg = RegisterTools.ParseRn(keyword2, true);
                if (reg != Rn.NOREG)
                {
                    // return (this.RegisterSwitchedOn(reg))
                    //    ? AsmTokenType.Register
                    //    : AsmTokenType.Register; //TODO
                    return AsmTokenType.Register; //TODO
                }
            }
            #endregion
            #region Test if keyword is an imm
            if (firstChar == '$')
            {
                return AsmTokenType.Constant;
            }
            #endregion
            #region Test if keyword is an instruction
            {
                (Mnemonic mnemonic, AttType type) = AsmSourceTools.ParseMnemonic_Att(keyword, true);
                if (mnemonic != Mnemonic.NONE)
                {
                    //TODO
                    // return (this.MnemonicSwitchedOn(mnemonic))
                    //     ? AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic
                    //     : AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.MnemonicOff;

                    return AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic;
                }
            }
            #endregion

            return this.type_.TryGetValue(keyword, out AsmTokenType tokenType) ? tokenType : AsmTokenType.UNKNOWN;
        }

        public AsmTokenType Get_Token_Type_Intel(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Assume(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            Mnemonic mnemonic = AsmSourceTools.ParseMnemonic(keyword, true);
            if (mnemonic != Mnemonic.NONE)
            {
                //TODO
                //return (this.MnemonicSwitchedOn(mnemonic))
                //    ? AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic
                //    : AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.MnemonicOff;

                return AsmSourceTools.IsJump(mnemonic) ? AsmTokenType.Jump : AsmTokenType.Mnemonic;
            }
            Rn reg = RegisterTools.ParseRn(keyword, true);
            if (reg != Rn.NOREG)
            {
                return AsmTokenType.Register;
            }
            return this.type_.TryGetValue(keyword, out AsmTokenType tokenType) ? tokenType : AsmTokenType.UNKNOWN;
        }

        public AssemblerEnum Get_Assembler(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Assume(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            return this.assembler_.TryGetValue(keyword, out AssemblerEnum value) ? value : AssemblerEnum.UNKNOWN;
        }

        /// <summary>
        /// get description for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an description. Keyword has to be in CAPITALS
        /// </summary>
        public string Get_Description(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            return this.description_.TryGetValue(keyword, out string description) ? description : string.Empty;
        }

        /// <summary>Get the collection of Keywords (in CAPITALS), but NOT mnemonics and registers</summary>
        public IEnumerable<string> Get_Keywords()
        {
            return this.type_.Keys;
        }

        /// <summary>
        /// Get architecture of the provided keyword. Keyword has to be in CAPITALS
        /// </summary>
        public Arch Get_Architecture(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            return this.arch_.TryGetValue(keyword, out Arch value) ? value : Arch.ARCH_NONE;
        }


        #endregion Public Methods

        #region Private Methods

        private static void LogInfo(string msg)
        {
            if (Instance.traceSource != null)
            {
                Instance.traceSource.TraceEvent(TraceEventType.Information, 0, msg);
            }
            Console.WriteLine($"INFO: {msg}");
        }

        private static void LogWarning(string msg)
        {
            if (Instance.traceSource != null)
            {
                Instance.traceSource.TraceEvent(TraceEventType.Warning, 0, msg);
            }
            Console.WriteLine($"WARNING: {msg}");
        }

        private static void LogError(string msg)
        {
            if (Instance.traceSource != null)
            {
                Instance.traceSource.TraceEvent(TraceEventType.Error, 0, msg);
            }
            Console.WriteLine($"ERROR: {msg}");
        }

        private void Init_Data(string path)
        {
            if (this.xmlData_ == null)
            {
                return;
            }

            // fill the dictionary with keywords
            {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
                string filename = Path.Combine(path, "AsmDudeData.xml");
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: AsmDudeTools:getXmlData: going to load file \"{0}\"", filename));
                try
                {
                    StringReader stringReader = new StringReader(File.ReadAllText(filename));
                    using (XmlReader reader = XmlReader.Create(stringReader, new XmlReaderSettings() { XmlResolver = null }))
                    {
                        this.xmlData_.Load(reader);
                    }
                }
                catch (FileNotFoundException)
                {
                    LogError("AsmDudeTools:Init_Data: could not find file \"" + filename + "\".");
                    return;
                }
                catch (XmlException)
                {
                    LogError("AsmDudeTools:Init_Data: xml error while reading file \"" + filename + "\".");
                    return;
                }
                catch (Exception e)
                {
                    LogError("AsmDudeTools:Init_Data: error while reading file \"" + filename + "\"." + e);
                    return;
                }
            }

            foreach (XmlNode node in this.xmlData_.SelectNodes("//misc"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found misc with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        this.type_[name] = AsmTokenType.Misc;
                        this.arch_[name] = Retrieve_Arch(node);
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
            foreach (XmlNode node in this.xmlData_.SelectNodes("//directive"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found directive with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        this.type_[name] = AsmTokenType.Directive;
                        this.arch_[name] = Retrieve_Arch(node);
                        this.assembler_[name] = Retrieve_Assembler(node);
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
            foreach (XmlNode node in this.xmlData_.SelectNodes("//register"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found register with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        //this.type_[name] = AsmTokenType.Register; //TODO why is this line removed?
                        this.arch_[name] = Retrieve_Arch(node);
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
            foreach (XmlNode node in this.xmlData_.SelectNodes("//userdefined1"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found user defined1 with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        this.type_[name] = AsmTokenType.UserDefined1;
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
            foreach (XmlNode node in this.xmlData_.SelectNodes("//userdefined2"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found user defined2 with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        this.type_[name] = AsmTokenType.UserDefined2;
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
            foreach (XmlNode node in this.xmlData_.SelectNodes("//userdefined3"))
            {
                if (node.Attributes != null)
                {
                    XmlAttribute nameAttribute = node.Attributes["name"];
                    if (nameAttribute == null)
                    {
                        LogWarning("AsmDudeTools:Init_Data: found user defined3 with no name");
                    }
                    else
                    {
                        string name = nameAttribute.Value.ToUpperInvariant();
                        this.type_[name] = AsmTokenType.UserDefined3;
                        this.description_[name] = Retrieve_Description(node);
                    }
                }
            }
        }

        private static Arch Retrieve_Arch(XmlNode node)
        {
            Contract.Requires(node != null);
            Contract.Assume(node != null);

            try
            {
                XmlAttribute archAttribute = node.Attributes["arch"];
                return (archAttribute == null) ? Arch.ARCH_NONE : ArchTools.ParseArch(archAttribute.Value, false, true);
            }
            catch (Exception)
            {
                return Arch.ARCH_NONE;
            }
        }

        private static AssemblerEnum Retrieve_Assembler(XmlNode node)
        {
            try
            {
                if (node != null)
                {
                    var attColl = node.Attributes;
                    if (attColl != null)
                    {
                        XmlAttribute archAttribute = attColl["tool"];
                        if (archAttribute != null)
                        {
                            return AsmSourceTools.ParseAssembler(archAttribute.Value, false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // do nothing
            }
            return AssemblerEnum.UNKNOWN;
        }

        private static string Retrieve_Description(XmlNode node)
        {
            try
            {
                XmlNode node2 = node.SelectSingleNode("./description");
                return (node2 == null) ? string.Empty : node2.InnerText.Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
        #endregion

        #region IDisposable Support

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~AsmDude2Tools()
        {
            this.Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                //this.threadPool_.Dispose();
            }
            // free native resources if there are any.
        }
        #endregion
    }
}