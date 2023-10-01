# AsmDude2
AsmDude2 represents a natural evolution from its predecessor, AsmDude. While AsmDude served as a 
single, all-encompassing plugin for VS2015/17/19, providing support for Assembly source code, 
AsmDude2 is built around a Language Server Protocol ([LSP](https://microsoft.github.io/language-server-protocol/))
and a lightweight Visual Studio extension (for VS2022), drawing its functionality from this LSP. 
Transitioning from a Visual Studio 2019 extension to one compatible with Visual Studio 2022 wasn't 
straightforward. Many of the features from the older AsmDude have yet to be ported, and some may 
never be. See the list of known issues and things still todo.

This extension can be found in the [visual studio extensions gallery](https://marketplace.visualstudio.com/items?itemName=Henk-JanLebbink.AsmDude2)
or download latest installer [AsmDude.vsix (v2.0.0.4)](https://github.com/HJLebbink/asm-dude/releases/download/v2.0.0.4/Asmdude2.v2-0-0-4.vsix). 

### Features

#### Syntax highlighting and Code Folding
AsmDude2 offers support for the following architectures: the instruction sets of x86 and x64, as well as 
SSE, AVX, AVX2, Xeon-Phi (Knights Corner), and AVX-512 instructions.
Most of the commonly used Masm directives covered, along with a selection of Nasm directives.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-syntax-highlighting.png?raw=true "Syntax highlighting")

#### Code Descriptions
When you hover over a mnemonic, you may receive a pop-up with descriptions. These descriptions 
can be modified and added by updating the AsmDudeData.xml file, which will be located alongside
the installed plugin binaries (.vsix). Finding the directory where plugins are installed can be
a bit challenging; you might want to try a location like C:\Users<user>\AppData\Local\Microsoft\VisualStudio\17.0\Extensions\AsmDude2\2.0.0.1\Server.
I kindly encourage you to share any updates you make.

![code-descriptions](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-descriptions.png?raw=true "Code descriptions")

Please note that the formatting of the pop-up does not display Markdown (see the known issues)

#### Code Completion 
While typing text, the completion lists will be refined to display the relevant 
language keywords. This applies to all keywords. However, please be aware that code suggestions 
may not be flawless at this stage; only valid code completions should be proposed.

![code-completion](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-code-completion.png?raw=true "Code Completion")

#### Signature Help
Signature Help, also referred to as Parameter Info, presents the method's signature in a tooltip when
a user enters the character marking the start of the parameter list (e.g., in C++, an opening parenthesis). 
As the user types a parameter and a parameter separator (usually a comma), 
the tooltip is refreshed to display the next parameter in bold.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-signature-help.png?raw=true "Signature Help")

## Disassembly Window in VS
Syntax highlighting in the disassembly window. No QuickInfo tooltips yet (see known issues)

## Where is the Source (Are you sure this is not a honeypot?!)
If you're reading this, you're probably an assembly programmer. However, if you're still interested 
in some C#, or you're just being cautious, you can run the extension from the source code. To do 
that, you'll need to have the Visual Studio 2022 SDK installed. To run the extension, press F5 or 
select the 'Debug > Start Debugging' option from the menu. This will launch a new instance of Visual 
Studio under the experimental environment.

### Things For a next release
* Update instructions for Sapphire Rapids
* Support AMX
* Make the LSP gracefully handle large (+10K lines)
* Restore hyperlinks in mnemonics (from AsmDude)
* Restore Label Analysis (from AsmDude)
* Restore AsmSim (from AsmDude)

### Feature Requests: (desire something - let me know)
* Support AMX
* Label rename assistance.
* Code formatting.
* Track flag influence. Select an opcode that uses a flag (as input), find the opcodes that produce this flag (as output). E.g.  select opcode cmovc or setc , highlight all opcodes such as btr, sal, sar, shl, shr, etc.
* Register rename assistance. Highly desirable but very challenging. E.g. rename GPR rdx to rbx, find which rdx, edx, dx, dl and dh will need to be renamed, check if renames will clash with existing occurances of rbx, ebx, bx, bl and bh.
* Arm support.
* Nasm macros syntax highlighting.
* Add support for MASM keyword "comment".
* Add syntax highlighting, statement completion and syntax checks for struct member fields.
* Disassembly window: show memory content from selected address (see [here](https://github.com/HJLebbink/asm-dude/issues/72)).
* Disassembly window: show memory content of the stack frame and stack pointer (see [here](https://github.com/HJLebbink/asm-dude/issues/72)).
* Add comment/uncomment functionality (see [Issue](https://github.com/HJLebbink/asm-dude/issues/76)).

### Updates:
* 21 September 2022: AsmDude2

### Known issues
* LSP client extension [for Visual Studio 2022](https://www.nuget.org/packages/Microsoft.VisualStudio.LanguageServer.Protocol.Extensions) does not honor Markdown. Please help me and vote for (https://stackoverflow.com/questions/77015711/popup-hover-with-markdown-from-a-language-server-protocol-lsp)
* Debug window does not trigger requests to the LSP. 
