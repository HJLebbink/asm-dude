namespace AsmDude
{
    using System;
    
    /// <summary>
    /// Helper class that exposes all GUIDs used across VS Package.
    /// </summary>
    internal sealed partial class PackageGuids
    {
        public const string guidMenuAndCommandsPkgString = "3c7c5abe-82ac-4a37-b077-0ff60e8b1fd3";
        public const string guidMenuAndCommandsCmdSetString = "19492bcb-32b3-4ec3-8826-d67cd5526653";
        public const string CustomMonikerString = "d53d7256-d44d-4245-bdd2-bfd22943659c";
        public static Guid guidMenuAndCommandsPkg = new Guid(guidMenuAndCommandsPkgString);
        public static Guid guidMenuAndCommandsCmdSet = new Guid(guidMenuAndCommandsCmdSetString);
        public static Guid CustomMoniker = new Guid(CustomMonikerString);
    }
    /// <summary>
    /// Helper class that encapsulates all CommandIDs uses across VS Package.
    /// </summary>
    internal sealed partial class PackageIds
    {
        public const int MyToolbar = 0x0101;
        public const int MyMenuGroup = 0x1010;
        public const int MyToolbarGroup = 0x1011;
        public const int MyMainToolbarGroup = 0x1012;
        public const int MyEditorCtxGroup = 0x1013;
        public const int cmdidMyCommand = 0x2001;
        public const int cmdidMyGraph = 0x2002;
        public const int cmdidMyZoom = 0x2003;
        public const int cmdidDynamicTxt = 0x2004;
        public const int cmdidDynVisibility1 = 0x2005;
        public const int cmdidDynVisibility2 = 0x2006;
        public const int Application = 0x0001;
    }
}
