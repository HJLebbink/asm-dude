using System;
using System.Diagnostics;
using System.Xml;
using System.Globalization;
using System.IO;
using System.Windows;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell;

using AsmTools;
using AsmDude.Tools;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using AsmDude.SyntaxHighlighting;
using Microsoft.VisualStudio.Utilities;

namespace AsmDude {

#pragma warning disable CS0162

    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AsmDudeTools : IDisposable {
        private XmlDocument _xmlData;
        private IDictionary<string, AsmTokenType> _type;
        private IDictionary<string, Arch> _arch;
        private IDictionary<string, string> _description;

        public AsmDudeTools() {
            //AsmDudeToolsStatic.Output(string.Format("INFO: AsmDudeTools constructor"));
            this.initData(); // load data for speed
        }

        #region Public Methods

        public ICollection<string> getKeywords() {
            if (this._type == null) initData();
            return this._type.Keys;
        }

        public AsmTokenType getTokenType(string keyword) {
            if (this._type == null) initData();

            AsmTokenType tokenType;
            if (this._type.TryGetValue(keyword.ToUpper(), out tokenType)) {
                return tokenType;
            }
            return AsmTokenType.UNKNOWN;
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string getUrl(string keyword) {
            // no need to pre-process this information.
            try {
                string keywordUpper = keyword.ToUpper();
                XmlNodeList all = this.getXmlData().SelectNodes("//*[@name=\"" + keywordUpper + "\"]");
                if (all.Count > 1) {
                    AsmDudeToolsStatic.Output(string.Format("WARNING: {0}:getUrl: multiple elements for keyword {1}.", this.ToString(), keywordUpper));
                }
                if (all.Count == 0) { // this situation happens when a keyword gets selected that does not have an url specified (such as labels)
                    //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:getUrl: no url for keyword \"{1}\".", this.ToString(), keywordUpper));
                    return "";
                } else {
                    XmlNode node1 = all.Item(0);
                    XmlNode node2 = node1.SelectSingleNode("./ref");
                    if (node2 == null) return "";
                    string text = node2.InnerText.Trim();
                    //AsmDudeToolsStatic.Output(string.Format("INFO: {0}:getUrl: keyword {1} yields {2}", this.ToString(), keyword, text));
                    return text;
                }
            } catch (Exception e) {
                AsmDudeToolsStatic.Output(string.Format("ERROR: {0}:getUrl: exception {1}.", this.ToString(), e.ToString()));
                return "";
            }
        }

        /// <summary>
        /// get url for the provided keyword. Returns empty string if the keyword does not exist or the keyword does not have an url.
        /// </summary>
        public string getDescription(string keyword) {
            string description;
            if (!this._description.TryGetValue(keyword, out description)) {
                description = "";
            }
            return description;
        }

        /// <summary>
        /// Determine whether the provided keyword is a jump mnemonic.
        /// </summary>
        public bool isJumpMnenomic(string keyword) {
            if (this._type == null) initData();
            AsmTokenType tokenType;
            if (this._type.TryGetValue(keyword.ToUpper(), out tokenType)) {
                return tokenType == AsmTokenType.Jump;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Determine whether the provided keyword is a mnemonic (but not a jump)
        /// </summary>
        public bool isMnemonic(string keyword) {
            if (this._type == null) initData();
            AsmTokenType tokenType;
            if (this._type.TryGetValue(keyword.ToUpper(), out tokenType)) {
                return tokenType == AsmTokenType.Mnemonic;
            } else {
                return false;
            }
        }


        /// <summary>
        /// Get architecture of the provided keyword
        /// </summary>
        public Arch getArchitecture(string keyword) {
            return this._arch[keyword.ToUpper()];
        }

        public void invalidateData() {
            this._xmlData = null;
            this._type = null;
            this._description = null;
        }


        #endregion Public Methods
        #region Private Methods


        private void initData() {
            this._type = new Dictionary<string, AsmTokenType>();
            this._arch = new Dictionary<string, Arch>();
            this._description = new Dictionary<string, string>();
       
            // fill the dictionary with keywords
            //AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);

            XmlDocument xmlDoc = this.getXmlData();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found misc with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found misc " + name);
                    this._type[name] = AsmTokenType.Misc;
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = retrieveDescription(node);
                }
            }

            foreach (XmlNode node in xmlDoc.SelectNodes("//directive")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found directive with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found directive " + name);
                    this._type[name] = AsmTokenType.Directive;
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = retrieveDescription(node);
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
                        this._type[name] = AsmTokenType.Mnemonic;
                    } else {
                        if (typeAttribute.Value.ToUpper().Equals("JUMP")) {
                            this._type[name] = AsmTokenType.Jump;
                        } else {
                            this._type[name] = AsmTokenType.Mnemonic;
                        }
                    }
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = retrieveDescription(node);
                }
            }
            foreach (XmlNode node in xmlDoc.SelectNodes("//register")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found register with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found register " + name);
                    this._type[name] = AsmTokenType.Register;
                    this._arch[name] = this.retrieveArch(node);
                    this._description[name] = retrieveDescription(node);
                }
            }
        }

        private Arch retrieveArch(XmlNode node) {
            try {
                var archAttribute = node.Attributes["arch"];
                if (archAttribute == null) {
                    return Arch.NONE;
                } else {
                    return AsmTools.AsmSourceTools.parseArch(archAttribute.Value.ToUpper());
                }
            } catch (Exception) {
                return Arch.NONE;
            }
        }

        private string retrieveDescription(XmlNode node) {
            try {
                XmlNode node2 = node.SelectSingleNode("./description");
                if (node2 == null) return "";
                string text = node2.InnerText.Trim();
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getDescription: keyword {1} yields {2}", this.ToString(), keyword, text));
                return text;
            } catch (Exception) {
                return "";
            }
        }

        private XmlDocument getXmlData() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData", this.ToString()));
            if (this._xmlData == null) {
                string filename = AsmDudeToolsStatic.getInstallPath() + "Resources" + Path.DirectorySeparatorChar + "AsmDudeData.xml";
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: AsmDudeTools:getXmlData: going to load file \"{0}\"", filename));
                try {
                    this._xmlData = new XmlDocument();
                    this._xmlData.Load(filename);
                } catch (FileNotFoundException) {
                    MessageBox.Show("ERROR: AsmTokenTagger: could not find file \"" + filename + "\".");
                } catch (XmlException) {
                    MessageBox.Show("ERROR: AsmTokenTagger: xml error while reading file \"" + filename + "\".");
                } catch (Exception e) {
                    MessageBox.Show("ERROR: AsmTokenTagger: error while reading file \"" + filename + "\"." + e);
                }
            }
            return this._xmlData;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    //
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~AsmDudeTools() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
#pragma warning restore CS0162
}