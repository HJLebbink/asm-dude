using Irony.Parsing;
using System.Diagnostics;
using System.Linq;

namespace asm_irony
{

    //http://www.codeproject.com/Articles/33250/Writing-Your-First-Visual-Studio-Language-Service





    // [Language("asm", "1.0", "Assembly Intel-Style Grammar")]
    public class AsmGrammar : Grammar
    {
        private readonly TerminalSet _skipTokensInPreview = new TerminalSet(); //used in token preview for conflict resolution
        public AsmGrammar()
        {
            this.GrammarComments = "Asm grammar.\r\n";

            #region Lexical structure

            NumberLiteral number = new NumberLiteral("NUMBER", NumberOptions.Default);
            IdentifierTerminal label = new IdentifierTerminal("LABEL", IdOptions.IsNotKeyword | IdOptions.AllowsEscapes | IdOptions.CanStartWithEscape);
            //RegexBasedTerminal label = new RegexBasedTerminal("LABEL", "[A-Za-z_][A-Za-z_0-9]+");

            CommentTerminal SingleLineComment = new CommentTerminal("SingleLineComment", ";", "#");
            this.NonGrammarTerminals.Add(SingleLineComment);

            KeyTerm COMMA = this.ToTerm(",", "COMMA");
            KeyTerm PLUS = this.ToTerm("+", "PLUS");
            KeyTerm MINUS = this.ToTerm("-", "MINUS");
            KeyTerm TIMES = this.ToTerm("*", "TIMES");

            ///<remarks>Bracket Square Left</remarks>
            KeyTerm BSL = this.ToTerm("[");
            ///<remarks>Bracket Square Right</remarks>
            KeyTerm BSR = this.ToTerm("]");
            ///<remarks>Bracket Left</remarks>
            KeyTerm BL = this.ToTerm("(");
            ///<remarks>Bracket Right</remarks>
            KeyTerm BR = this.ToTerm(")");





            this.MarkPunctuation("(", ")", "[", "]");


            #endregion Lexical structure




            #region NonTerminals

            NonTerminal line = new NonTerminal("line");
            NonTerminal directive = new NonTerminal("directive");

            NonTerminal label_def = new NonTerminal("label_def");
            NonTerminal label_def_opt = new NonTerminal("label_def_opt");

            NonTerminal instruction = new NonTerminal("instruction");
            NonTerminal instruction_opt = new NonTerminal("instruction_opt");

            #region Registers
            NonTerminal r8 = new NonTerminal("Reg8");
            NonTerminal r16 = new NonTerminal("Reg16");
            NonTerminal r32 = new NonTerminal("Reg32");
            NonTerminal r64 = new NonTerminal("Reg64");
            #endregion Registers

            NonTerminal m8 = new NonTerminal("Mem8");
            NonTerminal m16 = new NonTerminal("Mem16");
            NonTerminal m32 = new NonTerminal("Mem32");
            NonTerminal m64 = new NonTerminal("Mem64");

            NonTerminal mem_op = new NonTerminal("MemOp");
            //var mem_scale_index_32 = new NonTerminal("ScaleIdx32");
            NonTerminal mem_scale_index_64 = new NonTerminal("ScaleIdx64");
            //var mem_scale_index_bracket_32 = new NonTerminal("ScaleIdxBrac32");
            NonTerminal mem_scale_index_bracket_64 = new NonTerminal("ScaleIdxBrac64");

            // var mem_base_32 = new NonTerminal("Base32");
            NonTerminal mem_base_64 = new NonTerminal("Base64");
            NonTerminal mem_scale = new NonTerminal("Scale");
            NonTerminal mem_index_32 = new NonTerminal("Idx32");
            NonTerminal mem_index_64 = new NonTerminal("Idx64");
            NonTerminal mem_ptr = new NonTerminal("ptr");
            NonTerminal mem_disp = new NonTerminal("Disp");

            NonTerminal mnemomic_add = new NonTerminal("add");
            NonTerminal mnemomic_jmp = new NonTerminal("jmp");


            #endregion NonTerminals

            #region operators, punctuation and delimiters
            this.RegisterOperators(1, "+", "-");
            this.RegisterOperators(2, "*");
            #endregion

            #region comments
            this.MarkPunctuation(";", ",", "(", ")", "[", "]", ":");
            //this.MarkTransient(namespace_member_declaration, member_declaration);
            //this.AddTermsReportGroup("assignment", "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=");
            //this.AddToNoReportGroup("var", "const", "new");
            #endregion


            #region Rules

            this.Root = line;

            line.Rule = directive;
            line.Rule |= label_def_opt + instruction_opt;

            label_def_opt.Rule = this.Empty;
            label_def_opt.Rule |= label_def;

            label_def.Rule = label + ":";
            label_def.Rule |= label + "::";

            instruction_opt.Rule = this.Empty;
            instruction_opt.Rule |= instruction;

            #region Memory Operand
            m8.Rule = "byte" + mem_ptr + BSL + mem_op + BSR;
            m16.Rule = "word" + mem_ptr + BSL + mem_op + BSR;
            m32.Rule = "dword" + mem_ptr + BSL + mem_op + BSR;
            m64.Rule = "qword" + mem_ptr + BSL + mem_op + BSR;

            mem_ptr.Rule = this.Empty;
            mem_ptr.Rule |= "ptr";

            mem_op.Rule = mem_base_64 + PLUS + mem_scale_index_bracket_64 + mem_disp; //ABC
            mem_op.Rule |= mem_base_64 + mem_disp + PLUS + mem_scale_index_bracket_64; //ACB


            //mem_op.Rule |= mem_disp + PLUS + mem_base_64 + PLUS + mem_scale_index_bracket_64; //CAB
            //mem_op.Rule |= mem_disp + PLUS + mem_scale_index_bracket_64 + PLUS + mem_base_64; //CBA

            //PreferShiftHere() +

            //mem_op.Rule |=  mem_scale_index_bracket_64;  //B
            //mem_op.Rule |= mem_base_64;  not needed 
            //mem_op.Rule |= mem_disp; //C

            //mem_op.Rule |= mem_base_64 + PLUS + mem_scale_index_bracket_64; //AB
            //mem_op.Rule |= mem_scale_index_bracket_64 + PLUS + mem_base_64; //BA
            //mem_op.Rule |= mem_base_64 + PLUS + mem_disp; //AC
            //mem_op.Rule |= mem_disp + PLUS + mem_base_64; //CA


            mem_scale_index_bracket_64.Rule = BL + mem_scale_index_64 + BR;
            mem_scale_index_bracket_64.Rule |= mem_scale_index_64;

            mem_scale_index_64.Rule = mem_scale + TIMES + mem_index_64;
            mem_scale_index_64.Rule |= mem_index_64 + TIMES + mem_scale;
            mem_scale_index_64.Rule |= mem_index_64;

            mem_base_64.Rule = r64;

            mem_scale.Rule = this.ToTerm("0");
            mem_scale.Rule |= "1";
            mem_scale.Rule |= "2";
            mem_scale.Rule |= "4";
            mem_scale.Rule |= "8";

            mem_index_32.Rule = r32;

            mem_index_64.Rule = r64;

            mem_disp.Rule = PLUS + this.CustomActionHere(this.findTimesChar) + number;
            mem_disp.Rule |= MINUS + this.CustomActionHere(this.findTimesChar) + number;

            #endregion Memory Operand


            r8.Rule = this.ToTerm("al");
            r8.Rule |= "ah";

            r16.Rule = this.ToTerm("ax");

            r32.Rule = this.ToTerm("eax");

            r64.Rule = this.ToTerm("rax") | "rbx" | "rcx" | "rdx";


            #region Mnemonics
            instruction.Rule = mnemomic_add;
            instruction.Rule |= mnemomic_jmp;

            mnemomic_add.Rule = "add" + r8 + COMMA + r8;
            mnemomic_add.Rule |= "add" + r16 + COMMA + r16;
            mnemomic_add.Rule |= "add" + m16 + COMMA + r16;
            mnemomic_add.Rule |= "add" + r16 + COMMA + m16;

            mnemomic_jmp.Rule = "jmp" + r8;
            mnemomic_jmp.Rule |= "jmp" + label;
            mnemomic_jmp.Rule |= "jmp" + m32;

            #endregion Mnemonics

            #region Directive
            directive.Rule = this.ToTerm("align");
            directive.Rule |= "proc";

            #endregion Directive

            #endregion Rules


            label.ValidateToken += this.label_ValidateToken;
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

        private void label_ValidateToken(object sender, ParsingEventArgs e)
        {
            if (e.Context.CurrentToken.ValueString.Length > 4)
            {
                e.Context.CurrentToken = e.Context.CreateErrorToken("labels cannot be longer than 4 characters");
            }
        }
    }
}
