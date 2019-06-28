# Asm-Dude
Assembly syntax highlighting and code assistance for assembly source files and the disassembly window for Visual Studio 2015, 2017 and 2019. This extension can be found in the [visual studio extensions gallery](https://visualstudiogallery.msdn.microsoft.com/ff839577-2b68-416a-b761-72f9b1ca7c8e) or download latest installer [AsmDude.vsix (v1.9.6.8)](https://github.com/HJLebbink/asm-dude/releases/download/1.9.6.8/AsmDude.vsix). If assembly is too much of a hassle but you still want access to specific machine instructions, consider [Intrinsics-Dude](https://github.com/HJLebbink/intrinsics-dude).

### Features

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

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-doc-links-2.png?raw=true "Documentation Links")

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

#### Signature Help
Signature Help (also known as Parameter Info) displays the signature of a method in a tooltip when a user types the parameter list start character (eg in c++ an opening parenthesis). As a parameter and parameter separator (typically a comma) are typed, the tooltip is updated to show the next parameter in bold.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-signature-help.png?raw=true "Signature Help")

#### Label Analysis
Quick info tooltips for labels allow you to see where labels are defined. If labels are not defined, red error 
squiggles appear and an corresponding entry in the error list is added.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-undefined-labels.png?raw=true "Undefined Labels")

If labels are incorrectly defined more than once, quick info tooltips also provide information about these 
clashing label definitions. Red error squiggles appear and entries in the error list are added.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-label-clash.png?raw=true "Label Clashes")

## Disassembly Window in VS
QuickInfo tooltips, documentation links and syntax highlighting is available for the disassembly window.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-disassembly-popup.png?raw=true "Disassembly Window Support")

## Assembly Simulator (Available in version 2.0)

The assembly simulator interprets assembly code and allows to reason about assembly programs.

#### Multi-Valued Logics
The value of a flag or the value of a single bit in a register can either take the Boolean value of 1, we say the value is set, or it can take the Boolean value of 0, we say that the value is cleared. We assume that these two values are the only two values a bit may assume. When reasoning about these two values, other useful truth-values can be distinguished. These values represent the epistemic state a reasoner has about the value 0 and 1. Three types of ignorance, and an inconsistent state:
 
1. When you reason about the truth-value of a bit you try to determine in which world you live: a world in 
which the bit is set, or one in which the bit is cleared. You may conclude that you lack necessary information to determine the truth-value. In such a situation we say that the bit is UNKNOWN, denoted by a question mark '?'. The instruction "IN" retrieves a byte from I/O, the bits in that byte are UNKNOWN.
 
2. Another type of ignorance is introduced by instructions themselves. The specification may state that, for example, a flag is undefined after the execution of a specific instruction. The instruction AND sets the value of the auxiliary flag AF (obviously) either to a 0 or to a 1, yet the specification does not tell which one. In such a situation we say that a bit has the truth-value UNDEFINED, denoted by the letter 'U'.

3. Yet another type of ignorance is introduced by the bounded capacities of the reasoner. The theorem prover Z3 is used to establish the truth-value of bits. After a certain timeout the theorem prover gives up. In such a situation we say that the bit has the truth-value UNDETERMINED, denoted by a hyphen '-'.

4. The last truth-value indicates an inconsistent state (of the reasoner) when the reasoner establishes that a bit is set and at the same time it has information to conclude that the bit is cleared. This signals a state that cannot be reached. We say that a bit can have the truth-value INCONSISTENT, denoted by the letter 'X'.

#### Show Register Content
The register content before and after the current line is shown in QuickInfo tooltips when hovering over registers. "RCL EAX, 1" shifts the carry flag into position 0. The carry flag is undefined due to the previous BSF.

![show-register-content](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-register-content.png?raw=true "Register Content")

#### Semantic warning when using Undefined Values
Using undefined flags or registers in instruction most often signals a bug. Although it is conceivable that using undefined values is intended (For example in "XOR RAX, RAX"), still, you may want be warned about it. For example, the carry flag is used by RCL but CF has an undefined value.
	
![show-register-content](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-using-undefined.png?raw=true "Using Undefined Values")
	
#### Semantic warning for Redundant Instructions
When an instruction does not change the state of the registers and flags it writes to, give a redundancy warning. 

![redundant-instruction](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-redundant-instruction.png?raw=true "Redundant Instructions")

#### Semantic warning for Unreachable Instructions
Instructions can be unreachable due to conditional jumps that are never taken. If you were to request the truth-values of a register 
in an unreachable instruction the reasoner would conclude that you need an inconsistent state to reach the instruction. Something that cannot happen.

![unreachable-instruction](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-unreachable-instruction.png?raw=true "Unreachable Instructions")

#### Syntax Errors (found by the assembly simulator)
The Simulator was not build to find syntax errors, yet it does find some when traversing the file. Would be a waste not to feedback these errors.

![syntax-errors](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-syntax-errors.png?raw=true "Syntax Errors")

#### Register Content in Code Completions
When something is known about the register content, this information is shown in code completions.

![register-content-completions](https://github.com/HJLebbink/asm-dude/blob/master/Images/AsmDude-register-content-completions.png?raw=true "Register Content in Code Completions")


## Where is the Source (Are you sure this is not a honeypot?!)
If you are reading this you are most likely an assembly programmer, if you are still interested in some dirty c#, 
or you are just cautious, you can run the extension from source code. To do that, Visual Studio 2017 SDK needs to 
be installed. To run the extension, hit F5 or choose the Debug > Start Debugging menu command. A new instance of 
Visual Studio will launch under the experimental hive.

### Currently in development:
* Considering [Irony](https://irony.codeplex.com/) for proper parsing and error handling.
* ~~Considering [Z3](https://github.com/Z3Prover/z3) for proof tree induction to track propagation of register and flag state-change trough time.~~

### Known Issues:
* ~~Incomplete descriptions. E.g. PMOVSX, the source [html](https://hjlebbink.github.io/x86doc/html/PMOVSX.html) has a split table and only the first table is used as source.~~
* ~~MASM versions 6.0 and later treat all statement labels inside a procedure as local. AsmDude however does not yet honor this freedom, and will diligently complain about label clashes.~~

### Feature Requests: (desire something - let me know)
* ~~Documentation for opcodes. Hit F12 to get full official documentation of the selected opcode~~.
* ~~Proper Register Highlighting. E.g. When you select GPR rax, GPR al is also highlighted.~~
* ~~No code completion in remarks.~~
* ~~Improved syntax highlighting. Add label highlights.~~
* ~~Code completion for labels in jumps. Provide a list of existing labels from which one can choose.~~
* ~~Support for segment registers, debug registers and control registers.~~
* ~~Label analysis. When jumping to a label, check if the label exists. Check if labels are unique. Provide error squiggles if something is wrong.~~
* ~~Code folding for documentation blocks, folding for Masm procedure blocks and and Masm segments definitions.~~ 
* ~~Code completion restrictions. E.g. opcode movss can only be followed by an xmm register and not by a GPR such as rax.~~
* ~~Signature Help. Provide help which operands (type of registers, mem etc) are allowed for a given mnemonic).~~
* ~~Add syntax highlighting for AT&T syntax.~~
* ~~Syntax highlighting for the Debug/Windows/Disassembly view.~~
* Label rename assistance.
* Create new file item with .asm extension.
* Syntax highlighting in quickinfo tooltips, code folding block previews.
* Code formatting.
* Track flag influence. Select an opcode that uses a flag (as input), find the opcodes that produce this flag (as output). E.g.  select opcode cmovc or setc , highlight all opcodes such as btr, sal, sar, shl, shr, etc.
* Register rename assistance. Highly desirable but very challenging. E.g. rename GPR rdx to rbx, find which rdx, edx, dx, dl and dh will need to be renamed, check if renames will clash with existing occurances of rbx, ebx, bx, bl and bh.
* Arm support.
* Nasm macros syntax highlighting.
* Provide one (large) label graph for the complete solution such that label usage can be tracked throughout the solution.
* Add pragmas to disable warnings such as "#pragma AsmDude warning disable/restore".
* Add support for MASM keyword "comment".
* Add syntax highlighting, statement completion and syntax checks for struct member fields.
* Disassembly window: show memory content from selected address (see [here](https://github.com/HJLebbink/asm-dude/issues/72)).
* Disassembly window: show memory content of the stack frame and stack pointer (see [here](https://github.com/HJLebbink/asm-dude/issues/72)).
* Add comment/uncomment functionality (see [Issue](https://github.com/HJLebbink/asm-dude/issues/76)).

### Updates:
* 19 February 2016: Initial alpha release. Basic highlighting and descriptions for i386 instructions are available.
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
* 22 July 2016: Added Signature Help [v1.6.1.1]
* 13 Feb 2017: Added Performance Data for Skylake & Broadwell (Data from Agner Fog) [v1.7.4.0] 
* 16 June 2017: Added AT&T syntax support [v1.8.2.0]
* 25 Juli 2017: Added support for the VS Disassembly Window [v1.9.0.0]
* 04 June 2018: added Performance Data for Skylake-X [v1.9.5.0]
* 5 Januari 2019: Added support for VS2019 [v1.9.6.0]
