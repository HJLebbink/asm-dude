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

namespace AsmDude2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    using AsmDude2.Tools;

    using AsmTools;

    using Microsoft.VisualStudio.Shell;

    public sealed class AsmDude2Tools : IDisposable
    {
        private XmlDocument xmlData_;
        private IDictionary<string, AsmTokenType> type_;
        private IDictionary<string, AssemblerEnum> assembler_;
        private IDictionary<string, Arch> arch_;
        private IDictionary<string, string> description_;
  
        #region Singleton Stuff
        private static readonly Lazy<AsmDude2Tools> Lazy = new Lazy<AsmDude2Tools>(() => new AsmDude2Tools());

        public static AsmDude2Tools Instance { get { return Lazy.Value; } }
        #endregion Singleton Stuff

        /// <summary>
        /// Singleton pattern: use AsmDudeTools.Instance for the instance of this class
        /// </summary>
        private AsmDude2Tools()
        {
            AsmDudeToolsStatic.Output_INFO("AsmDude2Tools: constructor");

            ThreadHelper.ThrowIfNotOnUIThread();

            this.Init_Data();
         }

        #region Public Methods

        public AsmTokenType Get_Token_Type_Att(string keyword)
        {
            Contract.Requires(keyword != null);
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
                //return (this.RegisterSwitchedOn(reg))
                //    ? AsmTokenType.Register
                //    : AsmTokenType.Register; //TODO
                return AsmTokenType.Register;
            }
            return this.type_.TryGetValue(keyword, out AsmTokenType tokenType) ? tokenType : AsmTokenType.UNKNOWN;
        }

        public AssemblerEnum Get_Assembler(string keyword)
        {
            Contract.Requires(keyword != null);
            Contract.Requires(keyword == keyword.ToUpperInvariant());

            return this.assembler_.TryGetValue(keyword, out AssemblerEnum value) ? value : AssemblerEnum.UNKNOWN;
        }

        #endregion Public Methods

        #region Private Methods

        private void Init_Data()
        {
            this.type_ = new Dictionary<string, AsmTokenType>();
            this.arch_ = new Dictionary<string, Arch>();
            this.assembler_ = new Dictionary<string, AssemblerEnum>();
            this.description_ = new Dictionary<string, string>();
            // fill the dictionary with keywords
            XmlDocument xmlDoc = this.Get_Xml_Data();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found misc with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpperInvariant();
                    this.type_[name] = AsmTokenType.Misc;
                    this.arch_[name] = Retrieve_Arch(node);
                    this.description_[name] = Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//directive"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found directive with no name");
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
            foreach (XmlNode node in xmlDoc.SelectNodes("//register"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found register with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpperInvariant();
                    //this._type[name] = AsmTokenType.Register;
                    this.arch_[name] = Retrieve_Arch(node);
                    this.description_[name] = Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined1"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined1 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpperInvariant();
                    this.type_[name] = AsmTokenType.UserDefined1;
                    this.description_[name] = Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined2"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined2 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpperInvariant();
                    this.type_[name] = AsmTokenType.UserDefined2;
                    this.description_[name] = Retrieve_Description(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//userdefined3"))
            {
                XmlAttribute nameAttribute = node.Attributes["name"];
                if (nameAttribute == null)
                {
                    AsmDudeToolsStatic.Output_WARNING("AsmDudeTools:Init_Data: found userdefined3 with no name");
                }
                else
                {
                    string name = nameAttribute.Value.ToUpperInvariant();
                    this.type_[name] = AsmTokenType.UserDefined3;
                    this.description_[name] = Retrieve_Description(node);
                }
            }
        }

        private static Arch Retrieve_Arch(XmlNode node)
        {
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
                XmlAttribute archAttribute = node.Attributes["tool"];
                return (archAttribute == null) ? AssemblerEnum.UNKNOWN : AsmSourceTools.ParseAssembler(archAttribute.Value, false);
            }
            catch (Exception)
            {
                return AssemblerEnum.UNKNOWN;
            }
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

        private XmlDocument Get_Xml_Data()
        {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
            if (this.xmlData_ == null)
            {
                string filename = Path.Combine(AsmDudeToolsStatic.Get_Install_Path(), "Resources", "AsmDudeData.xml");
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: AsmDudeTools:getXmlData: going to load file \"{0}\"", filename));
                try
                {
                    this.xmlData_ = new XmlDocument() { XmlResolver = null };
                    System.IO.StringReader sreader = new System.IO.StringReader(File.ReadAllText(filename));
                    using (XmlReader reader = XmlReader.Create(sreader, new XmlReaderSettings() { XmlResolver = null }))
                    {
                        this.xmlData_.Load(reader);
                    }
                }
                catch (FileNotFoundException)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: could not find file \"" + filename + "\".");
                }
                catch (XmlException)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: xml error while reading file \"" + filename + "\".");
                }
                catch (Exception e)
                {
                    AsmDudeToolsStatic.Output_ERROR("AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
                }
            }
            return this.xmlData_;
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