using System.Linq;
using Irony.Parsing;
using System.Globalization;
using System.Diagnostics;

namespace asm_irony {

    [Language("asm2", "1.0", "Assembly Intel-Style Grammar")]
    public class AsmGrammar2 : Grammar {
        TerminalSet _skipTokensInPreview = new TerminalSet(); //used in token preview for conflict resolution
        public AsmGrammar2() {

            NumberLiteral number = new NumberLiteral("NUMBER", NumberOptions.Default);

            KeyTerm PLUS = ToTerm("+", "PLUS");
            KeyTerm TIMES = ToTerm("*", "TIMES");

            var mem_op = new NonTerminal("MemOp");
            var scaleIdx = new NonTerminal("ScaleIdx");
            var scaleIdxBr = new NonTerminal("ScaleIdxBrac64");

            var baseReg = new NonTerminal("Base");
            var scale = new NonTerminal("Scale");
            var idxReg = new NonTerminal("Idx");
            var disp = new NonTerminal("Disp");


            this.Root = mem_op;

            mem_op.Rule = "[" + baseReg + PLUS + scaleIdxBr + disp +"]";
            mem_op.Rule |= "[" + baseReg + disp + PLUS + scaleIdxBr + "]";

            scaleIdxBr.Rule = "(" + scaleIdx + ")";
            scaleIdxBr.Rule |= scaleIdx;

            scaleIdx.Rule = scale + TIMES + idxReg;
            scaleIdx.Rule |= idxReg + TIMES + scale;
            scaleIdx.Rule |= idxReg;

            baseReg.Rule = ToTerm("rax");
            scale.Rule = ToTerm("0") | "1" | "2" | "4" | "8";
            idxReg.Rule = ToTerm("rbx");

            disp.Rule = PLUS + CustomActionHere(this.findTimesChar) + number;
        }

        private void findTimesChar(ParsingContext context, CustomParserAction customAction) {

            string currentStr = context.CurrentParserInput.Term.Name;
            Debug.WriteLine("findTimesChar: current Term = " + currentStr);

            if (context.CurrentParserInput.Term == base.Eof) {
                return;
            }
            var scanner = context.Parser.Scanner;
            ParserAction action;

            scanner.BeginPreview();
            Token preview = scanner.GetToken();
            scanner.EndPreview(true);

            string previewStr = preview.Terminal.Name;
            Debug.WriteLine("findTimesChar: preview Term = " + previewStr);
            if (currentStr.Equals("NUMBER") && previewStr.Equals("TIMES")) {
                action = customAction.ReduceActions.First();
            } else {
                action = customAction.ShiftActions.First(a => a.Term.Name == "NUMBER");
            }
            action.Execute(context);
        }
    }
}
