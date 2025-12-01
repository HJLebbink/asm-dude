/***************************************************************************
 
Copyright (c) Microsoft Corporation. All rights reserved.
THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

***************************************************************************/

using System;

namespace Microsoft.Samples.VisualStudio.MenuCommands
{
	/// <summary>
	/// This class is used to expose the list of the IDs of the commands implemented
	/// by this package. This list of IDs must match the set of IDs defined inside the
	/// Buttons section of the VSCT file.
	/// </summary>
	internal static class PkgCmdIDList
	{
		// Now define the list a set of public static members.
		public const int asmDudeCommand1 = 0x2001;
		public const int asmDudeCommand2 = 0x2002;
		public const int asmDudeCommand3 = 0x2003;
	}
}
