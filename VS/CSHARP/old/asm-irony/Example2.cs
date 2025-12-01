using Irony.Parsing;
using System.Diagnostics;
using System.Linq;

namespace asm_irony
{

    [Language("asm2", "1.0", "Assembly Intel-Style Grammar")]
    public class AsmGrammar2 : Grammar
    {
        private readonly TerminalSet _skipTokensInPreview = new TerminalSet(); //used in token preview for conflict resolution
        public AsmGrammar2()
        {

            NumberLiteral number = new NumberLiteral("NUMBER", NumberOptions.Default);

            KeyTerm PLUS = this.ToTerm("+", "PLUS");
            KeyTerm TIMES = this.ToTerm("*", "TIMES");

            NonTerminal mem_op = new NonTerminal("MemOp");
            NonTerminal scaleIdx = new NonTerminal("ScaleIdx");
            NonTerminal scaleIdxBr = new NonTerminal("ScaleIdxBrac64");

            NonTerminal baseReg = new NonTerminal("Base");
            NonTerminal scale = new NonTerminal("Scale");
            NonTerminal idxReg = new NonTerminal("Idx");
            NonTerminal disp = new NonTerminal("Disp");


            this.Root = mem_op;

            mem_op.Rule = "[" + baseReg + PLUS + scaleIdxBr + disp + "]";
            mem_op.Rule |= "[" + baseReg + disp + PLUS + scaleIdxBr + "]";

            scaleIdxBr.Rule = "(" + scaleIdx + ")";
            scaleIdxBr.Rule |= scaleIdx;

            scaleIdx.Rule = scale + TIMES + idxReg;
            scaleIdx.Rule |= idxReg + TIMES + scale;
            scaleIdx.Rule |= idxReg;

            baseReg.Rule = this.ToTerm("rax");
            scale.Rule = this.ToTerm("0") | "1" | "2" | "4" | "8";
            idxReg.Rule = this.ToTerm("rbx");

            disp.Rule = PLUS + this.CustomActionHere(this.findTimesChar) + number;
        }

        private void findTimesChar(ParsingContext context, CustomParserAction customAction)
        {

            string currentStr = context.CurrentParserInput.Term.Name;
            Debug.WriteLine("findTimesChar: current Term = " + currentStr);

            if (context.CurrentParserInput.Term == this.Eof)
            {
                return;
            }
            Scanner scanner = context.Parser.Scanner;
            ParserAction action;

            scanner.BeginPreview();
            Token preview = scanner.GetToken();
            scanner.EndPreview(true);

            string previewStr = preview.Terminal.Name;
            Debug.WriteLine("findTimesChar: preview Term = " + previewStr);
            if (currentStr.Equals("NUMBER") && previewStr.Equals("TIMES"))
            {
                action = customAction.ReduceActions.First();
            }
            else
            {
                action = customAction.ShiftActions.First(a => a.Term.Name == "NUMBER");
            }
            action.Execute(context);
        }
    }
}
