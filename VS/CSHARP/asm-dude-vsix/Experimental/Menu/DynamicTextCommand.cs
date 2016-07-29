/***************************************************************************
 
Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Globalization;

namespace AsmDude
{
    /// <summary>
    /// This class implements a very specific type of command: this command will count the
    /// number of times the user has clicked on it and will change its text to show this count.
    /// </summary>
    internal class DynamicTextCommand : OleMenuCommand
    {
        // Counter of the clicks.
        private int clickCount;

        /// <summary>
        /// This is the function that is called when the user clicks on the menu command.
        /// It will check that the selected object is actually an instance of this class and
        /// increment its click counter.
        /// </summary>
        private static void ClickCallback(object sender, EventArgs args)
        {
            DynamicTextCommand cmd = sender as DynamicTextCommand;
            if (null != cmd)
            {
                cmd.clickCount++;
            }
        }

        /// <summary>
        /// Creates a new DynamicTextCommand object with a specific CommandID and base text.
        /// </summary>
        public DynamicTextCommand(CommandID id, string text) :
            base(new EventHandler(ClickCallback), id, text)
        {
        }

        /// <summary>
        /// If a command is defined with the TEXTCHANGES flag in the VSCT file and this package is
        /// loaded, then Visual Studio will call this property to get the text to display.
        /// </summary>
        public override string Text
        {
            get
            {
                //return string.Format(CultureInfo.CurrentCulture, VsPackage.ResourceManager.GetString("DynamicTextFormat"), base.Text, clickCount);
                return "";
            }
            set { base.Text = value; }
        }
    }
}
