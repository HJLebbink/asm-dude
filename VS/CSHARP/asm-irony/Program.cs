
using Irony.Interpreter;
using Irony.Parsing;


namespace asm_irony
{
    internal class Program
    {
        private static void Main(string[] args)
        {

            Grammar grammar = new AsmGrammar2();
            LanguageData language = new LanguageData(grammar);
            LanguageRuntime runtime = new LanguageRuntime(language);
            CommandLine commandLine = new CommandLine(runtime);
            commandLine.Run();
        }
    }
}
