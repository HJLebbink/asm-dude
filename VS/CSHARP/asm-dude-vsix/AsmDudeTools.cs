using System;
using System.Diagnostics;
using System.Xml;
using System.Globalization;
using System.IO;
using System.Windows;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using AsmTools;


namespace AsmDude {

    public static class AsmDudeToolsStatic {

        public static CompositionContainer getCompositionContainer() {
            AssemblyCatalog catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            CompositionContainer container = new CompositionContainer(catalog);
            return container;
        }

        public static string getInstallPath() {
            try {
                string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string filenameDll = "AsmDude.dll";
                return fullPath.Substring(0, fullPath.Length - filenameDll.Length);
            } catch (Exception) {
                return "";
            }
        }

        public static System.Windows.Media.Color convertColor(System.Drawing.Color color) {
            return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static ImageSource bitmapFromUri(Uri bitmapUri) {
            var bitmap = new BitmapImage();
            try {
                bitmap.BeginInit();
                bitmap.UriSource = bitmapUri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            } catch (Exception e) {
                Debug.WriteLine("WARNING: bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        public static void Output(string msg) {
            // Get the output window
            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            // Ensure that the desired pane is visible
            var paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
            IVsOutputWindowPane pane;
            outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
            outputWindow.GetPane(paneGuid, out pane);
            pane.OutputString(string.Format(CultureInfo.CurrentCulture, "{0}", msg + Environment.NewLine));
        }

        /// <summary>
        /// Get all labels with context info containing in the provided text
        /// </summary>
        public static IDictionary<string, string> getLabels(string text) {
            IDictionary<string, string> result = new Dictionary<string, string>();
            int nLabels = 0;
            int lineNumber = 1; // start counting at one since that is what VS does
            foreach (string line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: getLabels: str=\"{0}\"", str));

                Tuple<bool, int, int> labelPos = AsmTools.AsmSourceTools.getLabelPos(line);
                if (labelPos.Item1) {
                    int labelBeginPos = labelPos.Item2;
                    int labelEndPos = labelPos.Item3;
                    string label = line.Substring(labelBeginPos, labelEndPos - labelBeginPos);
                    nLabels++;
                    string description = "LINE " + lineNumber + ": " + line.Substring(0, Math.Min(line.Length, 100));
                    result.Add(label, description);
                    //AsmDudeToolsStatic.Output(string.Format("INFO: getLabels: label=\"{0}\"; description=\"{1}\".", label, description));
                }
                lineNumber++;
            }
            return result;
        }


        /// <summary>
        /// Get all labels with context info containing in the provided text
        /// </summary>
        public static IDictionary<string, string> getLabels(ITextBuffer text) {
            return AsmDudeToolsStatic.getLabels(text.CurrentSnapshot.GetText());
        }

        public static string getKeywordStr(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);

                int beginPos = t.Item1;
                int endPos = t.Item2;
                return line.Substring(beginPos, endPos);
            }
            return null;
        }

        public static TextExtent? getKeyword(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);

                int beginPos = t.Item1 + startLine;
                int endPos = t.Item2 + startLine;
                return new TextExtent(new SnapshotSpan(bufferPosition.Value.Snapshot, beginPos, endPos - beginPos), true);
            }
            return null;
        }

        public static string getLabelDescription(string label, string text) {
            int lineNumber = 1; // start counting at one since that is what VS does

            string result = "";
            foreach (string line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)) {

                // find first occurrence of a colon
                int posColon = -1;

                for (int pos = 0; pos < line.Length; ++pos) {
                    char c = line[pos];
                    if (c == ':') {
                        posColon = pos;
                        break;
                    } else if (AsmTools.AsmSourceTools.isRemarkChar(c)) {
                        break;
                    }
                }
                if (posColon > 0) {
                    string labelLocal = line.Substring(0, posColon).TrimStart();
                    if (labelLocal.Equals(label)) {
                        //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getLabelDescription: label=\"{1}\"", this.ToString(), label));
                        if (result.Length > 0) result += System.Environment.NewLine;
                        result += "LINE " + lineNumber + ": " + line.Substring(0, Math.Min(line.Length, 100));
                    }
                }
                lineNumber++;
            }
            return result;
        }

        public static string getLabelDescription(string label, ITextBuffer text) {
            return AsmDudeToolsStatic.getLabelDescription(label, text.CurrentSnapshot.GetText());
        }

        public static bool isAllUpper(string input) {
            for (int i = 0; i < input.Length; i++) {
                if (Char.IsLetter(input[i]) && !Char.IsUpper(input[i])) {
                    return false;
                }
            }
            return true;
        }
    }

    [Export]
    public class AsmDudeTools {
        private XmlDocument _xmlData;
        private IDictionary<string, TokenType> _type;
        private IDictionary<string, Arch> _arch;
        private IDictionary<string, string> _description;


        public AsmDudeTools() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            this.initData(); // load data for speed
        }

        public ICollection<string> getKeywords() {
            if (this._type == null) initData();
            return this._type.Keys;
        }

        public TokenType getTokenType(string keyword) {
            if (this._type == null) initData();

            TokenType tokenType;
            string k2 = keyword.ToUpper();
            if (!this._type.TryGetValue(k2, out tokenType)) {
                tokenType = TokenType.UNKNOWN;
            }
            return tokenType;
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
                    Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "WARNING: {0}:getUrl: multiple elements for keyword {1}.", this.ToString(), keywordUpper));
                }
                if (all.Count == 0) {
                    //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getUrl: no elements for keyword {1}.", this.ToString(), keywordUpper));
                    return "";
                } else {
                    XmlNode node1 = all.Item(0);
                    XmlNode node2 = node1.SelectSingleNode("./ref");
                    if (node2 == null) return "";
                    string text = node2.InnerText.Trim();
                    //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getUrl: keyword {1} yields {2}", this.ToString(), keyword, text));
                    return text;
                }
            } catch (Exception) {
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
            TokenType tokenType;
            string k2 = keyword.ToUpper();
            if (this._type.TryGetValue(k2, out tokenType)) {
                return tokenType == TokenType.Jump;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Determine whether the provided keyword is a mnemonic (but not a jump)
        /// </summary>
        public bool isMnemonic(string keyword) {
            if (this._type == null) initData();
            TokenType tokenType;
            string k2 = keyword.ToUpper();
            if (this._type.TryGetValue(k2, out tokenType)) {
                return tokenType == TokenType.Mnemonic;
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

        #region private stuff

        private void initData() {
            this._type = new Dictionary<string, TokenType>();
            this._arch = new Dictionary<string, Arch>();
            this._description = new Dictionary<string, string>();
       
            // fill the dictionary with keywords
            AsmDudeToolsStatic.getCompositionContainer().SatisfyImportsOnce(this);
            XmlDocument xmlDoc = this.getXmlData();
            foreach (XmlNode node in xmlDoc.SelectNodes("//misc")) {
                var nameAttribute = node.Attributes["name"];
                if (nameAttribute == null) {
                    Debug.WriteLine("WARNING: AsmTokenTagger: found misc with no name");
                } else {
                    string name = nameAttribute.Value.ToUpper();
                    //Debug.WriteLine("INFO: AsmTokenTagger: found misc " + name);
                    this._type[name] = TokenType.Misc;
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
                    this._type[name] = TokenType.Directive;
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
                        this._type[name] = TokenType.Mnemonic;
                    } else {
                        if (typeAttribute.Value.ToUpper().Equals("JUMP")) {
                            this._type[name] = TokenType.Jump;
                        } else {
                            this._type[name] = TokenType.Mnemonic;
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
                    this._type[name] = TokenType.Register;
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
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getXmlData: going to load file \"{1}\"", this.ToString(), filename));
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

        #endregion
    }
}