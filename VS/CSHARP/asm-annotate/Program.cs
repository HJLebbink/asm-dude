using System;

namespace asm_annotate
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            ParserXed.ParseFile(@".\data\icelake.csv");
        }
    }
}
