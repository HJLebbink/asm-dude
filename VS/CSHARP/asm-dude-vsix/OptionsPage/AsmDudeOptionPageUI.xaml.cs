using AsmDude.Tools;
using AsmTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;



namespace AsmDude.OptionsPage {
    /// <summary>
    /// Interaction logic for AsmDudeOptionPageUI.xaml
    /// </summary>
    public partial class AsmDudeOptionsPageUI : UserControl {

        public AsmDudeOptionsPageUI() {
            InitializeComponent();
        }

        #region Asm Documentation
        public bool useAsmDoc {
            get { return (useAsmDoc_UI.IsChecked.HasValue) ? useAsmDoc_UI.IsChecked.Value : false; }
            set { useAsmDoc_UI.IsChecked = value; }
        }

        public string asmDocUrl {
            get { return asmDocUrl_UI.Text; }
            set { asmDocUrl_UI.Text = value; }
        }
        #endregion Asm Documentation

        #region Code Folding
        public bool useCodeFolding {
            get { return (useCodeFolding_UI.IsChecked.HasValue) ? useCodeFolding_UI.IsChecked.Value : false; }
            set { useCodeFolding_UI.IsChecked = value; }
        }

        public bool isDefaultCollaped {
            get { return (isDefaultCollaped_UI.IsChecked.HasValue) ? isDefaultCollaped_UI.IsChecked.Value : false; }
            set { isDefaultCollaped_UI.IsChecked = value; }
        }

        public string beginTag {
            get { return beginTag_UI.Text; }
            set { beginTag_UI.Text = value; }
        }

        public string endTag {
            get { return endTag_UI.Text; }
            set { endTag_UI.Text = value; }
        }
        #endregion Code Folding

        #region Syntax Highlighting

        private const string cat = "Colors used for Syntax Highlighting";

        public bool useSyntaxHighlighting {
            get { return (useSyntaxHighlighting_UI.IsChecked.HasValue) ? useSyntaxHighlighting_UI.IsChecked.Value : false; }
            set { useSyntaxHighlighting_UI.IsChecked = value; }
        }

        public AssemblerEnum usedAssembler {
            get {
                if (usedAssemblerMasm_UI.IsChecked.HasValue && usedAssemblerMasm_UI.IsChecked.Value) return AssemblerEnum.MASM;
                if (usedAssemblerNasm_UI.IsChecked.HasValue && usedAssemblerNasm_UI.IsChecked.Value) return AssemblerEnum.NASM;
                return AssemblerEnum.MASM;
            }
            set {
                switch (value) {
                    case AssemblerEnum.MASM:
                        usedAssemblerMasm_UI.IsChecked = true;
                        usedAssemblerNasm_UI.IsChecked = false;
                        break;
                    case AssemblerEnum.NASM:
                        usedAssemblerMasm_UI.IsChecked = false;
                        usedAssemblerNasm_UI.IsChecked = true;
                        break;
                }
            }
        }

        /// [DefaultValue(System.Drawing.KnownColor.Blue)]
       // public System.Drawing.Color colorMnemonic {
       //     get { return colorMnemonic_UI.; }
       //     set { useSyntaxHighlighting_UI.colorMnemonic_UI = value; }
       // }

        /*
        [Category(cat)]
        [Description("Register")]
        [DisplayName("Register")]
        [DefaultValue(System.Drawing.KnownColor.DarkRed)]
        public System.Drawing.Color _colorRegister { get; set; }

        [Category(cat)]
        [Description("Remark")]
        [DisplayName("Remark")]
        [DefaultValue(System.Drawing.KnownColor.Green)]
        public System.Drawing.Color _colorRemark { get; set; }

        [Category(cat)]
        [Description("Directive")]
        [DisplayName("Directive")]
        [DefaultValue(System.Drawing.KnownColor.Magenta)]
        public System.Drawing.Color _colorDirective { get; set; }

        [Category(cat)]
        [Description("Constant")]
        [DisplayName("Constant")]
        [DefaultValue(System.Drawing.KnownColor.Chocolate)]
        public System.Drawing.Color _colorConstant { get; set; }

        [Category(cat)]
        [Description("Jump")]
        [DisplayName("Jump")]
        [DefaultValue(System.Drawing.KnownColor.Blue)]
        public System.Drawing.Color _colorJump { get; set; }

        [Category(cat)]
        [Description("Label")]
        [DisplayName("Label")]
        [DefaultValue(System.Drawing.KnownColor.OrangeRed)]
        public System.Drawing.Color _colorLabel { get; set; }

        [Category(cat)]
        [Description("Misc")]
        [DisplayName("Misc")]
        [DefaultValue(System.Drawing.KnownColor.DarkOrange)]
        public System.Drawing.Color _colorMisc { get; set; }
        */
        #endregion Syntax Highlighting

    }
}
