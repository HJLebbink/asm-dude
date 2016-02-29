
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell.Interop;

using GelUtilities = Microsoft.Internal.VisualStudio.PlatformUI.Utilities;

namespace AsmDude.OptionsPage {
    /// <summary>
    /// This class implements UI for the Custom options page.
    /// An OptionsPageCustom object provides the backing data.
    /// </summary>
    public class OptionsCompositeControl : UserControl
    {
        #region Fields

        private PictureBox pictureBox;
        private Button buttonChooseImage;
        private Button buttonClearImage;
        private OptionsPageCustom customOptionsPage;

        // ImageMoniker data
        private PictureBox bitmapPictureBox;

        #endregion

        #region Constructors

        public OptionsCompositeControl()
        {
            // This call is required by the Windows.Forms Form Designer.
            InitializeComponent();
        }

        #endregion

        #region Methods

        #region Component Designer generated code
        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.buttonChooseImage = new System.Windows.Forms.Button();
            this.buttonClearImage = new System.Windows.Forms.Button();
            this.bitmapPictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bitmapPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox
            // 
            this.pictureBox.Location = new System.Drawing.Point(16, 16);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(264, 120);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.pictureBox.TabIndex = 0;
            this.pictureBox.TabStop = false;
            // 
            // buttonChooseImage
            // 
            this.buttonChooseImage.Location = new System.Drawing.Point(16, 152);
            this.buttonChooseImage.Name = "buttonChooseImage";
            this.buttonChooseImage.Size = new System.Drawing.Size(112, 23);
            this.buttonChooseImage.TabIndex = 1;
            this.buttonChooseImage.Text = global::AsmDude.Resources.ChooseImageButtonText;
            this.buttonChooseImage.Click += new System.EventHandler(this.OnChooseImage);
            // 
            // buttonClearImage
            // 
            this.buttonClearImage.Location = new System.Drawing.Point(160, 152);
            this.buttonClearImage.Name = "buttonClearImage";
            this.buttonClearImage.Size = new System.Drawing.Size(96, 23);
            this.buttonClearImage.TabIndex = 2;
            this.buttonClearImage.Text = global::AsmDude.Resources.ButtonClearImageText;
            this.buttonClearImage.Click += new System.EventHandler(this.OnClearImage);
            // 
            // bitmapPictureBox
            // 
            this.bitmapPictureBox.Location = new System.Drawing.Point(290, 16);
            this.bitmapPictureBox.Name = "bitmapPictureBox";
            this.bitmapPictureBox.Size = new System.Drawing.Size(32, 32);
            this.bitmapPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.bitmapPictureBox.TabIndex = 3;
            this.bitmapPictureBox.TabStop = false;
            // 
            // OptionsCompositeControl
            // 
            this.AllowDrop = true;
            this.Controls.Add(this.buttonClearImage);
            this.Controls.Add(this.buttonChooseImage);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.bitmapPictureBox);
            this.Name = "OptionsCompositeControl";
            this.Size = new System.Drawing.Size(355, 195);
            // 
            this.Load += new System.EventHandler(this.LoadMoniker);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bitmapPictureBox)).EndInit();
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// Called whenever the 'Choose Image' button is pressed. This function summons an
        /// OpenFileDialog, and allows the user to select an image file to display in the 
        /// PictureBox. Once the file has been selected, the PictureBox is refreshed.
        /// </summary>
        private void OnChooseImage(object sender, EventArgs e)
        {
            var openImageFileDialog = new OpenFileDialog();

            if (openImageFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (customOptionsPage != null)
                {
                    customOptionsPage.CustomBitmap = openImageFileDialog.FileName;
                }
                RefreshImage();
            }
        }

        /// <summary>
        /// Called whenever the 'Clear Image' button is pressed. This function sets 
        /// the filename to null and refreshes the PictureBox. No image is displayed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClearImage(object sender, EventArgs e)
        {
            if (customOptionsPage != null)
            {
                customOptionsPage.CustomBitmap = null;
            }
            RefreshImage();
        }
        

        /// <summary>
        /// Refresh PictureBox Image data. Display the desired image(or nothing).
        /// </summary>
        /// <remarks>The image is reloaded from the file specified by CustomBitmap (full path to the file).</remarks>
        private void RefreshImage()
        {
            if (customOptionsPage == null)
            {
                return;
            }

            string fileName = customOptionsPage.CustomBitmap;
            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    // Avoid using Image.FromFile() method for image loading because it locks the file.
                    using (FileStream lStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    {
                        pictureBox.Image = Image.FromStream(lStream);
                    }
                }
                catch (IOException)
                {
                    pictureBox.Image = null;
                }
            }
            else
            {
                pictureBox.Image = null;
            }
        }

        /// <summary>
        /// Gets or sets the reference to the underlying OptionsPage object.
        /// </summary>
        public OptionsPageCustom OptionsPage
        {
            get
            {
                return customOptionsPage;
            }
            set
            {
                customOptionsPage = value;
                RefreshImage();
            }
        }

        /// <summary>
        /// Load and display an image moniker in a PictureBox
        /// </summary>
        private void LoadMoniker(object sender, EventArgs e)
        {
            /*
            IVsImageService2 imageService = (IVsImageService2)AsmOptionsPagePackage.GetGlobalService(typeof(SVsImageService));

            ImageAttributes attributes = new ImageAttributes
            {
                StructSize = Marshal.SizeOf(typeof(ImageAttributes)),
                ImageType = (uint)_UIImageType.IT_Bitmap,
                Format = (uint)_UIDataFormat.DF_WinForms,
                LogicalWidth = 32,
                LogicalHeight = 32,
                // Desired RGBA color, don't set IAF_Background below unless you also use this
                Background = 0xFFFFFFFF,
                // (uint)(_ImageAttributesFlags.IAF_RequiredFlags | _ImageAttributesFlags.IAF_Background)
                Flags = (uint)_ImageAttributesFlags.IAF_RequiredFlags,
            };

            IVsUIObject uIObj = imageService.GetImage(KnownMonikers.Search, attributes);

            bitmapPictureBox.Image = (Bitmap)GelUtilities.GetObjectData(uIObj);
            */
        }
        #endregion
    }
}
