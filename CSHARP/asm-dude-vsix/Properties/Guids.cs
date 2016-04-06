using System;

namespace AsmDude {
    internal static class Guids {

        public const string GuidPackage_str = "9CD7D1D2-7075-4B2C-8DE1-8D374DEDF278";
        public static readonly Guid GuidPackage = new Guid("{"+GuidPackage_str+"}");

        public const string GuidOptionsPageCodeCompletion = "86FF506D-AE5E-4068-9012-FA7AA9E8CA45";
        public const string GuidOptionsPageSyntaxHighlighting = "0A46DCB0-E87E-4021-97C4-3F7830FE8812";
        public const string GuidOptionsPageCodeFolding = "27CE417C-CABE-4F7B-9AE1-7CC13F14BA11";
        public const string GuidOptionsPageAsmDoc = "1DC9C90E-8E25-4F3B-88E6-84CCEBFDD7FA";
        public const string GuidOptionsPageKeywordHighlighting = "92E6BE17-EDD6-4758-99D2-C03E75E36580";



        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Guid guidMenuAndCommandsPkg = new Guid("{3C7C5ABE-82AC-4A37-B077-0FF60E8B1FD3}");
        public const string guidMenuAndCommandsPkg_string = "3C7C5ABE-82AC-4A37-B077-0FF60E8B1FD3";

        public static readonly Guid guidMenuAndCommandsCmdSet = new Guid("{19492BCB-32B3-4EC3-8826-D67CD5526653}");

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Guid guidGenericCmdBmp = new Guid("{0A4C51BD-3239-4370-8869-16E0AE8C0A46}");
    }
}
