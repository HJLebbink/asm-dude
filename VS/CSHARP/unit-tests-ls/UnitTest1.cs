using LanguageServerLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace unit_tests_ls
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1() {

            string input = "abc def ghi\ndef aaa bbb";

            Stream sender = new MemoryStream(Encoding.ASCII.GetBytes(input));
            Stream reader = new MemoryStream();
            var ls = new LanguageServer(sender, reader);

            StreamReader reader2 = new StreamReader(reader);
            string output = reader2.ReadToEnd();

            Console.WriteLine($"output {output}");
            
        }
    }
}
