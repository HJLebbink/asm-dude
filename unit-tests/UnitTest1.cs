using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace unit_tests {
    [TestClass]
    public class UnitTest1 {
        [TestMethod]
        public void TestMethod1() {
            int expected = 1;
            int actual = 2;

            Assert.AreEqual(expected, actual, "Account not debited correctly");
        }
    }
}
