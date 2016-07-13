# Asm-Dude
Assembly syntax highlighting, code completion and folding for Visual Studio 2015. This extension can be found in the [visual studio extensions gallery](https://visualstudiogallery.msdn.microsoft.com/ff839577-2b68-416a-b761-72f9b1ca7c8e) or download latest installer [AsmDude.vsix (v1.5.3.0)](https://github.com/HJLebbink/asm-dude/releases/download/1.5.3.0/AsmDude.vsix)

###Features

#### Syntax highlighting and Descriptions
The following architectures are supported: the instruction sets of the x86 and the x64, but also 
SSE, AVX, AVX2, Xeon-Phi (Knights Corner) instructions with their descriptions are provided.
Most of the regularly used Masm directives are supported and some Nasm directives. 

Highlighting and descriptions are also provided for labels.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-label-usage.png?raw=true "Label Usage")

If you are not happy with highlighting or the descriptions. Mnemonics and descriptions can be added and changed by updating 
the AsmDudeData.xml file that will be stored next to the binaries when installing the plugin (.vsix). 
The directory where plugins are installed can be difficult to find, try something as 
C:\Users\<user>\AppData\Local\Microsoft\VisualStudio\14.0\Extensions. Please consider sharing your updates.

#### Documentation Links  
If you hover the mouse over a mnemonic when the CTRL button is down, mnemonics may become underlined, indicating 
that an html reference exists that points to a documentation webpage.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-doc-links.png?raw=true "Documentation Links")

#### Code Completion 
While typing texts completion lists will narrow down to the relevant language keywords. This works for all keywords and 
labels. Code suggestion may not be perfect yet, in the sense that only valid code completions should be suggested. 
For example, after a call or jump mnemonic you expect a label, thus the list of completions will only show labels to 
choose from. 

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-code-completion.png?raw=true "Code Completion")

#### Code Folding
The keywords *#region* and *#endregion* lets you specify a block of code that you can expand or collapse when using 
the outlining feature of the Visual Studio Code Editor. In longer code files, it is convenient to be able to 
collapse or hide one or more regions so that you can focus on the part of the file that you are currently 
working on. 

#### Label Analysis
Quick info tooltips for labels allow you to see where labels are defined. If labels are not defined, red error 
squiggles appear and an corresponding entry in the error list is added.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-undefined-labels.png?raw=true "Undefined Labels")

If labels are incorrectly defined more than once, quick info tooltips also provide information about these 
clashing label definitions. Red error squiggles appear and entries in the error list are added.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-label-clash.png?raw=true "Label Clashes")

## Where is the Source
If you are reading this you are most likely an assembly programmer, if you are still interested in some dirty c# 
you can run the extension from source code. To do that, Visual Studio 2015 SDK needs to be installed. 
To run the extension, hit F5 or choose the Debug > Start Debugging menu command. A new instance of Visual 
Studio will launch under the experimental hive.

###Currently in development:
* Considering [Irony](https://irony.codeplex.com/) for proper parsing and error handling.
* Considering [Z3](https://github.com/Z3Prover/z3) for proof tree induction to track propagation of register and flag state-change trough time.

###Feature Requests: (desire something - let me know)
* ~~Documentation for opcodes. Hit F12 to get full official documentation of the selected opcode~~.
* ~~Proper Register Highlighting. E.g. When you select GPR rax, GPR al is also highlighted.~~
* ~~No code completion in remarks.~~
* ~~Improved syntax highlighting. Add label highlights.~~
* ~~Code completion for labels in jumps. Provide a list of existing labels from which one can choose.~~
* ~~Support for segment registers, debug registers and control registers.~~
* ~~Label analysis. When jumping to a label, check if the label exists. Check if labels are unique. Provide error squiggles if something is wrong.~~
* ~~Code folding for documentation blocks, folding for Masm procedure blocks and and Masm segments definitions.~~ 
* ~~Code completion restrictions. E.g. opcode movss can only be followed by an xmm register and not by a GPR such as rax.~~
* Signature Help. Provide help which operands (type of registers, mem etc) are allowed for a given mnemonic).
* Label rename assistance.
* Create new file item with .asm extension.
* Add syntax highlighting for AT&T syntax.
* Syntax highlighting in quickinfo tooltips.
* Code formatting.
* Syntax highlighting for the Debug/Windows/Disassembly view.
* Track flag influence. Select an opcode that uses a flag (as input), find the opcodes that produce this flag (as output). E.g.  select opcode cmovc or setc , highlight all opcodes such as btr, sal, sar, shl, shr, etc.
* Register rename assistance. Highly desirable but very challenging. E.g. rename GPR rdx to rbx, find which rdx, edx, dx, dl and dh will need to be renamed, check if renames will clash with existing occurances of rbx, ebx, bx, bl and bh.
* Arm support.
* Nasm macros syntax highlighting.
* Provide one (large) label graph for the complete solution such that label usage can be tracked throughout the solution.
* Add pragmas to disable warnings such as "#pragma AsmDude warning disable/restore".
* Add support for MASM keyword "comment".

###Updates:
* 19 February 2016: Initial alpha release. Basic highlighting and descriptions for i368 instructions are available.
* 20 February 2016: Added highlighting and descriptions for SSE, AVX, AVX2 instructions.
* 21 February 2016: Added .vsix installer
* 22 February 2016: Added .vsix installer to the visual studio extensions gallery
* 23 February 2016: Added code completion [v1.1]
* 26 February 2016: Added code folding [v1.2]
* 2 March 2016: Added option pages for customizations. [v1.4]
* 7 March 2016: Added documentation for opcodes for CTRL + left mouse. [v1.4.2]
* 9 March 2016: Added register highlighting. [v1.4.4]
* 14 March 2016: bugfixes and anoyances fixes [v1.4.6]
* 21 March 2016: Added code completion for labels in jumps [v1.4.8]
* 8 May 2016: Added Label analysis [v1.5.0.0]

