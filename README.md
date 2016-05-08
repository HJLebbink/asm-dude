# Asm-Dude
Assembly syntax highlighting, code completion and folding for Visual Studio 2015

This extension can be found in the [visual studio extensions gallery](https://visualstudiogallery.msdn.microsoft.com/ff839577-2b68-416a-b761-72f9b1ca7c8e) or download latest installer [AsmDude.vsix (v1.4.8)](https://github.com/HJLebbink/asm-dude/releases/download/v1.4.8/AsmDude.vsix)

##Features

### Syntax highlighting and Descriptions
Currently the following architectures are supported: the instruction sets of the i386 and the x64, but also 
SSE, AVX, AVX2, Xeon-Phi (Knights Landing) instructions with their descriptions are provided.
Most of the regularly used Masm directives are supported and some Nasm directives. If you are not happy 
with highlighting or the descriptions. Mnemonics and descriptions can be added and changed by updating 
the AsmDudeData.xml file that will be stored next to the binaries when installing the plugin (.vsix). 
The directory where plugins are installed can be difficult to find, try something as 
C:\Users\<user>\AppData\Local\Microsoft\VisualStudio\14.0\Extensions. Please consider sharing your updates.

### Documentation Links  
When the Ctrl butten is down while hovering over a mnemonic, the mnemonic may become underlined, indicating 
that an html reference exists that points to a documentation webpage.

### Code Completion 
While typing a completion list narrows down to the relevant language keywords. It may not be perfect yet but
the functionality that it provides is useful. For example, after a call or jump mnemonic a label is expected, 
the list completions will then show the list of labels to choose from. 

### Label Analysis
Quick info tooltips show whether labels are used or where they are used. 


## Where is the Source
If you are reading this you are most likely an assembly programmer, if you are still interested in some c# 
you can run the extension from source code. To do that, Visual Studio 2015 SDK needs to be installed. 
To run the extension, hit F5 or choose the Debug > Start Debugging menu command. A new instance of Visual 
Studio will launch under the experimental hive.

###Currently in development:
* Irony Parser for proper parsing and error handling.
* Track propagation of register and flag state-change trough time (under certain assumptions such as no "jmp GPR" etc). Provide warning squiggles if brances are never taken etc. 

###Feature Requests: (desire something - let me know)
* ~~Documentation for opcodes. Hit F12 to get full official documentation of the selected opcode~~.
* ~~Proper Register Highlighting. E.g. When you select GPR rax, GPR al is also highlighted.~~
* ~~No code completion in remarks.~~
* ~~Improved syntax highlighting. Add label highlights.~~
* ~~Code completion for labels in jumps. Provide a list of existing labels from which one can choose.~~
* ~~Support for segment registers.~~
* ~~Label analysis. When jumping to a label, check if the label exists. Check if labels are unique. Provide error squiggles if something is wrong.~~
* Label rename assistance.
* Create new file item with .asm extension.
* Code folding for documentation blocks.
* Add syntax highlighting for AT&T syntax.
* Syntax highlighting in quickinfo tooltips.
* Code formatting.
* Syntax highlighting for the Debug/Windows/Disassembly view.
* Code completion restrictions. E.g. opcode movss can only be followed by an xmm register and not by a GPR such as rax.
* Track flag influence. Select an opcode that uses a flag (as input), find the opcodes that produce this flag (as output). E.g.  select opcode cmovc or setc , highlight all opcodes such as btr, sal, sar, shl, shr, etc.
* Register rename assistance. Highly desirable but very challenging. E.g. rename GPR rdx to rbx, find which rdx, edx, dx, dl and dh will need to be renamed, check if renames will clash with existing occurances of rbx, ebx, bx, bl and bh.
* Arm support.
* Nasm macros syntax highlighting

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
* 8 May 2016: Added Label analysis [v1.5.0]

