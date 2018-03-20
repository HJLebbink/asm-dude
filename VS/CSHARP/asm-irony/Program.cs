
using Irony.Parsing;
using Irony.Interpreter;


namespace asm_irony
{
    class Program {
        static void Main(string[] args) {

            Grammar grammar = new AsmGrammar2();
            var language = new LanguageData(grammar);
            var runtime = new LanguageRuntime(language);
            var commandLine = new CommandLine(runtime);
            commandLine.Run();
        }
    }
}
