using System;
using System.Diagnostics;
using System.Xml;
using System.Globalization;
using System.IO;
using System.Windows;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using AsmDude.Properties;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AsmDude {


    public static class AsmDudeToolsStatic {

        public static CompositionContainer getCompositionContainer() {
            AssemblyCatalog catalog = new AssemblyCatalog(System.Reflection.Assembly.GetExecutingAssembly());
            CompositionContainer container = new CompositionContainer(catalog);
            return container;
        }

        public static string getInstallPath() {
            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string filenameDll = "AsmDude.dll";
            return fullPath.Substring(0, fullPath.Length - filenameDll.Length);
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

        public static string getRelatedRegister(string reg) {
            switch (reg.ToUpper()) {
                case "RAX":
                case "EAX":
                case "AX":
                case "AL":
                case "AH":
                    return "\\b(RAX|EAX|AX|AH|AL)\\b";
                case "RBX":
                case "EBX":
                case "BX":
                case "BL":
                case "BH":
                    return "\\b(RBX|EBX|BX|BH|BL)\\b";
                case "RCX":
                case "ECX":
                case "CX":
                case "CL":
                case "CH":
                    return "\\b(RCX|ECX|CX|CH|CL)\\b";
                case "RDX":
                case "EDX":
                case "DX":
                case "DL":
                case "DH":
                    return "\\b(RDX|EDX|DX|DH|DL)\\b";
                case "RSI":
                case "ESI":
                case "SI":
                case "SIL":
                    return "\\b(RSI|ESI|SI|SIL)\\b";
                case "RDI":
                case "EDI":
                case "DI":
                case "DIL":
                    return "\\b(RDI|EDI|DI|DIL)\\b";
                case "RBP":
                case "EBP":
                case "BP":
                case "BPL":
                    return "\\b(RBP|EBP|BP|BPL)\\b";
                case "RSP":
                case "ESP":
                case "SP":
                case "SPL":
                    return "\\b(RSP|ESP|SP|SPL)\\b";
                case "R8":
                case "R8D":
                case "R8W":
                case "R8B":
                    return "\\b(R8|R8D|R8W|R8B)\\b";
                case "R9":
                case "R9D":
                case "R9W":
                case "R9B":
                    return "\\b(R9|R9D|R9W|R9B)\\b";
                case "R10":
                case "R10D":
                case "R10W":
                case "R10B":
                    return "\\b(R10|R10D|R10W|R10B)\\b";
                case "R11":
                case "R11D":
                case "R11W":
                case "R11B":
                    return "\\b(R11|R11D|R11W|R11B)\\b";
                case "R12":
                case "R12D":
                case "R12W":
                case "R12B":
                    return "\\b(R12|R12D|R12W|R12B)\\b";
                case "R13":
                case "R13D":
                case "R13W":
                case "R13B":
                    return "\\b(R13|R13D|R13W|R13B)\\b";
                case "R14":
                case "R14D":
                case "R14W":
                case "R14B":
                    return "\\b(R14|R14D|R14W|R14B)\\b";
                case "R15":
                case "R15D":
                case "R15W":
                case "R15B":
                    return "\\b(R15|R15D|R15W|R15B)\\b";

                default: return reg;
            }
        }

        public static bool isRegister(string keyword) {

            //TODO  get this info from AsmDudeData.xml
            switch (keyword.ToUpper()) {
                case "RAX":
                case "EAX":
                case "AX":
                case "AL":
                case "AH":

                case "RBX":
                case "EBX":
                case "BX":
                case "BL":
                case "BH":

                case "RCX":
                case "ECX":
                case "CX":
                case "CL":
                case "CH":

                case "RDX":
                case "EDX":
                case "DX":
                case "DL":
                case "DH":

                case "RSI":
                case "ESI":
                case "SI":
                case "SIL":

                case "RDI":
                case "EDI":
                case "DI":
                case "DIL":

                case "RBP":
                case "EBP":
                case "BP":
                case "BPL":

                case "RSP":
                case "ESP":
                case "SP":
                case "SPL":

                case "R8":
                case "R8D":
                case "R8W":
                case "R8B":

                case "R9":
                case "R9D":
                case "R9W":
                case "R9B":

                case "R10":
                case "R10D":
                case "R10W":
                case "R10B":

                case "R11":
                case "R11D":
                case "R11W":
                case "R11B":

                case "R12":
                case "R12D":
                case "R12W":
                case "R12B":

                case "R13":
                case "R13D":
                case "R13W":
                case "R13B":

                case "R14":
                case "R14D":
                case "R14W":
                case "R14B":

                case "R15":
                case "R15D":
                case "R15W":
                case "R15B":

                case "MM0":
                case "MM1":
                case "MM2":
                case "MM3":
                case "MM4":
                case "MM5":
                case "MM6":
                case "MM7":

                case "XMM0":
                case "XMM1":
                case "XMM2":
                case "XMM3":
                case "XMM4":
                case "XMM5":
                case "XMM6":
                case "XMM7":

                case "XMM8":
                case "XMM9":
                case "XMM10":
                case "XMM11":
                case "XMM12":
                case "XMM13":
                case "XMM14":
                case "XMM15":

                case "YMM0":
                case "YMM1":
                case "YMM2":
                case "YMM3":
                case "YMM4":
                case "YMM5":
                case "YMM6":
                case "YMM7":

                case "YMM8":
                case "YMM9":
                case "YMM10":
                case "YMM11":
                case "YMM12":
                case "YMM13":
                case "YMM14":
                case "YMM15":

                case "ZMM0":
                case "ZMM1":
                case "ZMM2":
                case "ZMM3":
                case "ZMM4":
                case "ZMM5":
                case "ZMM6":
                case "ZMM7":

                case "ZMM8":
                case "ZMM9":
                case "ZMM10":
                case "ZMM11":
                case "ZMM12":
                case "ZMM13":
                case "ZMM14":
                case "ZMM15":

                case "ZMM16":
                case "ZMM17":
                case "ZMM18":
                case "ZMM19":
                case "ZMM20":
                case "ZMM21":
                case "ZMM22":
                case "ZMM23":

                case "ZMM24":
                case "ZMM25":
                case "ZMM26":
                case "ZMM27":
                case "ZMM28":
                case "ZMM29":
                case "ZMM30":
                case "ZMM31":

                    return true;
                default:
                    return false;
            }
        }
    }


    [Export]
    public class AsmDudeTools {
        private XmlDocument _xmlData;

        public AsmDudeTools() {
            //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: Entering constructor for: {0}", this.ToString()));
        }

        public XmlDocument getXmlData() {
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

        public string getUrl(string keyword) {
            string keywordUpper = keyword.ToUpper();
            XmlDocument doc = this.getXmlData();
            XmlNodeList all = doc.SelectNodes("//*[@name=\""+ keywordUpper + "\"]");
            if (all.Count > 1) {
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "WARNING: {0}:getUrl: multiple elements for keyword {1}.", this.ToString(), keywordUpper));
            }
            if (all.Count == 0) {
                //Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getUrl: no elements for keyword {1}.", this.ToString(), keywordUpper));
                return null;
            } else {
                XmlNode node1 = all.Item(0);
                XmlNode node2 = node1.SelectSingleNode("./ref");
                if (node2 == null) return null;
                string reference = node2.InnerText.Trim();
                Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "INFO: {0}:getUrl: keyword {1} yields {2}", this.ToString(), keywordUpper, reference));
                return reference;
            }
        }

        //public void invalidateXmlData() {
        //    this._xmlData = null;
        //}
    }
}