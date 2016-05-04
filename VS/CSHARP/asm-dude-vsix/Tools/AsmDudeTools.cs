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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text.Tagging;
using AsmDude.SyntaxHighlighting;

namespace AsmDude {

    public static class AsmDudeToolsStatic {

        public static CompositionContainer getCompositionContainer() {
            AssemblyCatalog catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            CompositionContainer container = new CompositionContainer(catalog);
            return container;
        }

        public static string GetFileName(ITextBuffer buffer) {
            Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer bufferAdapter;
            buffer.Properties.TryGetProperty(typeof(Microsoft.VisualStudio.TextManager.Interop.IVsTextBuffer), out bufferAdapter);
            if (bufferAdapter != null) {
                var persistFileFormat = bufferAdapter as IPersistFileFormat;
                string ppzsFilename = null;
                uint iii;
                if (persistFileFormat != null) {
                    persistFileFormat.GetCurFile(out ppzsFilename, out iii);
                }
                return ppzsFilename;
            } else {
                return null;
            }
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
                AsmDudeToolsStatic.Output("WARNING: bitmapFromUri: could not read icon from uri " + bitmapUri.ToString() + "; " + e.Message);
            }
            return bitmap;
        }

        /// <summary>
        /// Cleans the provided line by removing multiple white spaces and cropping if the line is too long
        /// </summary>
        public static string cleanup(string line) {
            string cleanedString = System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " ");
            if (cleanedString.Length > AsmDudePackage.maxNumberOfCharsInToolTips) {
                return cleanedString.Substring(0, AsmDudePackage.maxNumberOfCharsInToolTips-3) + "...";
            } else {
                return cleanedString;
            }
        }

        public static void Output(string msg) {
            IVsOutputWindow outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            string msg2 = string.Format(CultureInfo.CurrentCulture, "{0}", msg.Trim() + Environment.NewLine);
            if (outputWindow == null) {
                Debug.Write(msg2);
            } else {
                Guid paneGuid = Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid;
                IVsOutputWindowPane pane;
                outputWindow.CreatePane(paneGuid, "AsmDude", 1, 0);
                outputWindow.GetPane(paneGuid, out pane);
                pane.OutputString(msg2);
                pane.Activate();
            }
        }

        /// <summary>
        /// Get all labels with context info contained in the provided text
        /// </summary>
        public static IDictionary<string, string> getLabelDescriptions(string text) {
            IDictionary<string, string> result = new Dictionary<string, string>();
            int lineNumber = 1; // start counting at one since that is what VS does
            foreach (string line in text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)) {
                //AsmDudeToolsStatic.Output(string.Format("INFO: getLabels: str=\"{0}\"", str));

                Tuple<bool, int, int> labelPos = AsmTools.AsmSourceTools.getLabelDefPos(line);
                if (labelPos.Item1) {
                    int labelBeginPos = labelPos.Item2;
                    int labelEndPos = labelPos.Item3;
                    string label = line.Substring(labelBeginPos, labelEndPos - labelBeginPos);
                    string description = "";

                    if (result.ContainsKey(label)) {
                        description += description + Environment.NewLine;
                    }
                    description += AsmDudeToolsStatic.cleanup("LINE " + lineNumber + ": " + line);
                    if (result.ContainsKey(label)) {
                        AsmDudeToolsStatic.Output(string.Format("INFO: multiple label definitions for label \"{0}\".", label));
                    } else {
                        result.Add(label, description);
                    }

                    //AsmDudeToolsStatic.Output(string.Format("INFO: getLabels: label=\"{0}\"; description=\"{1}\".", label, description));
                }
                lineNumber++;
            }
            return result;
        }

        public static string getKeywordStr(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);

                int beginPos = t.Item1;
                int endPos = t.Item2;
                int length = endPos - beginPos;

                string result = line.Substring(beginPos, length);
                //AsmDudeToolsStatic.Output("INFO: getKeyword: \"" + result + "\".");
                return result;
            }
            return null;
        }

        public static TextExtent? getKeyword(SnapshotPoint? bufferPosition) {

            if (bufferPosition != null) {
                string line = bufferPosition.Value.GetContainingLine().GetText();
                int startLine = bufferPosition.Value.GetContainingLine().Start;
                int currentPos = bufferPosition.Value.Position;

                Tuple<int, int> t = AsmTools.AsmSourceTools.getKeywordPos(currentPos - startLine, line);
                //AsmDudeToolsStatic.Output(string.Format("INFO: getKeywordPos: beginPos={0}; endPos={1}.", t.Item1, t.Item2));

                int beginPos = t.Item1 + startLine;
                int endPos = t.Item2 + startLine;
                int length = endPos - beginPos;

                SnapshotSpan span = new SnapshotSpan(bufferPosition.Value.Snapshot, beginPos, length);
                //AsmDudeToolsStatic.Output("INFO: getKeyword: \"" + span.GetText() + "\".");
                return new TextExtent(span, true);
            }
            return null;
        }

        /// <summary>
        /// Find the previous keyword (if any) that exists BEFORE the provided triggerPoint, and the provided start.
        /// Eg. qqqq xxxxxx yyyyyyy zzzzzz
        ///     ^             ^
        ///     |begin        |end
        /// the previous keyword is xxxxxx
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static string getPreviousKeyword(SnapshotPoint begin, SnapshotPoint end) {
            // return getPreviousKeyword(begin.GetContainingLine.)
            if (end == 0) return "";

            int beginLine = begin.GetContainingLine().Start;
            int beginPos = begin.Position - beginLine;
            int endPos = end.Position - beginLine;
            return AsmSourceTools.getPreviousKeyword(beginPos, endPos, begin.GetContainingLine().GetText());
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
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AsmDudeTools {
        private XmlDocument _xmlData;
        private IDictionary<string, AsmTokenType> _type;
        private IDictionary<string, Arch> _arch;
        private IDictionary<string, string> _description;
        private ErrorListProvider _errorListProvider;

        public AsmDudeTools() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
            this.initData(); // load data for speed
        }

        public ICollection<string> getKeywords() {
            if (this._type == null) initData();
            return this._type.Keys;
        }

        public AsmTokenType getTokenType(string keyword) {
            if (this._type == null) initData();

            AsmTokenType tokenType;
            string k2 = keyword.ToUpper();
            if (!this._type.TryGetValue(k2, out tokenType)) {
                tokenType = AsmTokenType.UNKNOWN;
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
            string k2 = keyword.ToUpper();
            if (this._type.TryGetValue(k2, out tokenType)) {
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
            string k2 = keyword.ToUpper();
            if (this._type.TryGetValue(k2, out tokenType)) {
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

        #region private stuff

        private void initData() {
            this._type = new Dictionary<string, AsmTokenType>();
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

        public ErrorListProvider GetErrorListProvider() {

            if (this._errorListProvider == null) {
                IServiceProvider serviceProvider;
                if (true) {
                    serviceProvider = new ServiceProvider(Package.GetGlobalService(typeof(Microsoft.VisualStudio.OLE.Interop.IServiceProvider)) as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
                } else {
                    serviceProvider = Package.GetGlobalService(typeof(IServiceProvider)) as ServiceProvider;
                }
                this._errorListProvider = new ErrorListProvider(serviceProvider);
                this._errorListProvider.ProviderName = "Asm Errors";
                this._errorListProvider.ProviderGuid = new Guid(EnvDTE.Constants.vsViewKindCode);
            }
            return this._errorListProvider;
        }

        #endregion
    }
}