// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace unit_tests_asm_tools
{
    using System.Collections.Generic;

    using AsmTools;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Test_AsmLog
    {
        // The startup banner must survive a quiet (Warn) log level so it can mark each session and create
        // the VS language-server pane — but a hard Off must still silence everything.
        [TestMethod]
        public void Banner_BypassesThreshold_ButHardOffSilencesIt()
        {
            var captured = new List<AsmLogEntry>();
            AsmLogLevel previous = AsmLog.Threshold;
            try
            {
                AsmLog.ClearSinks();
                AsmLog.AddSink(captured.Add);

                AsmLog.Threshold = AsmLogLevel.Warn;
                AsmLog.Info("TEST", "ordinary info");      // below Warn -> dropped
                AsmLog.Banner("TEST", "startup banner");   // forced -> emitted

                Assert.AreEqual(1, captured.Count, "the banner must emit at Warn while ordinary Info does not");
                Assert.AreEqual(AsmLogLevel.Info, captured[0].Level, "the banner is Info severity (not a fake Warning)");
                Assert.AreEqual("startup banner", captured[0].Message);

                captured.Clear();
                AsmLog.Threshold = AsmLogLevel.Off;
                AsmLog.Banner("TEST", "should be silenced");
                Assert.AreEqual(0, captured.Count, "a hard Off silences even the forced banner");
            }
            finally
            {
                AsmLog.ClearSinks();
                AsmLog.Threshold = previous;
            }
        }
    }
}
