# AsmDude2
AsmDude2 represents a natural evolution from its predecessor, AsmDude. While AsmDude served as a 
single, all-encompassing plugin for VS2015/17/19, providing support for Assembly source code, 
AsmDude2 is built around a Language Server Protocol ([LSP](https://microsoft.github.io/language-server-protocol/))
and a lightweight Visual Studio extension (for VS2022), drawing its functionality from this LSP. 
Transitioning from a Visual Studio 2019 extension to one compatible with Visual Studio 2022 wasn't 
straightforward. Many of the features from the older AsmDude have yet to be ported, and some may 
never be. See the list of known issues and things still todo.

This extension can be found in the [visual studio extensions gallery](https://visualstudiogallery.msdn.microsoft.com/ff839577-2b68-416a-b761-72f9b1ca7c8e)
or download latest installer [AsmDude.vsix (v2.0.0.2)](https://github.com/HJLebbink/asm-dude/releases/download/1.9.6.14/AsmDude.vsix). 

### Features

#### Syntax highlighting and Code Folding
The following architectures are supported: the instruction sets of the x86 and the x64, but also 
SSE, AVX, AVX2, Xeon-Phi (Knights Corner), AVX-512 instructions are provided.
Most of the regularly used Masm directives are supported and some Nasm directives. 

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-syntax-highlighting.png?raw=true "Syntax highlighting")

#### Code Descriptions
When you hover over a mnemonic you may get a popup with descriptions. Descriptions can be added and changed by updating 
the AsmDudeData.xml file that will be stored next to the binaries when installing the plugin (.vsix). 
The directory where plugins are installed can be difficult to find, try something as C:\Users\<user>\AppData\Local\Microsoft\VisualStudio\17.0\Extensions\AsmDude2\2.0.0.1\Server
Please consider sharing your updates.

![code-descriptions](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-descriptions.png?raw=true "Code descriptions")

Observe that the layout of the popup does not render Markdown (see known issues). 

#### Code Completion 
While typing texts completion lists will narrow down to the relevant language keywords. This works for all keywords and 
labels. Code suggestion may not be perfect yet, in the sense that only valid code completions should be suggested. 

![code-completion](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-code-completion.png?raw=true "Code Completion")

#### Signature Help
Signature Help (also known as Parameter Info) displays the signature of a method in a tooltip when a user types the parameter list start character (eg in c++ an opening parenthesis). As a parameter and parameter separator (typically a comma) are typed, the tooltip is updated to show the next parameter in bold.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude2-signature-help.png?raw=true "Signature Help")

## Disassembly Window in VS
Syntax highlighting in the disassembly window. No QuickInfo tooltips yet (see known issues)

## Where is the Source (Are you sure this is not a honeypot?!)
If you are reading this you are most likely an assembly programmer, if you are still interested in some dirty c#, 
or you are just cautious, you can run the extension from source code. To do that, Visual Studio 2022 SDK needs to 
be installed. To run the extension, hit F5 or choose the Debug > Start Debugging menu command. A new instance of 
Visual Studio will launch under the experimental hive.

### Things For a next release
* Update instructions for Sapphire Rapids
* Support AMX
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
