# Asm-Dude
Assembly syntax highlighting, code completion and folding for Visual Studio 2015

This extension can be found in the [visual studio extensions gallery](https://visualstudiogallery.msdn.microsoft.com/ff839577-2b68-416a-b761-72f9b1ca7c8e) or download latest installer [AsmDude.vsix (v1.4)](https://github.com/HJLebbink/asm-dude/releases/download/v1.4/AsmDude.vsix)

###Run Source Code
To run the extension from source code, Visual Studio 2015 SDK needs to be installed. To run the extension, hit F5 or choose the Debug > Start Debugging menu command. A new instance of Visual Studio will launch under the experimental hive.

###Customization:
Not happy with the highlighting or the descriptions. Mnemonics and descriptions can be added and changed by updating the AsmDudeData.xml file that will be stored next to the binaries when installing the plugin (.vsix). The directory where plugins are installed can be difficult to find, try something as C:\Users\<user>\AppData\Local\Microsoft\VisualStudio\14.0\Extensions. Please consider sharing your updates.

###Feature Requests: (Desire something, let me know)
* Documentation for opcodes. Hit F12 to get full official documentation of the selected opcode.
* Label analysis. When jumping to a label, check if the label exists. Check if labels are unique.
* Code folding for documentation blocks.
* Improved syntax highlighting.  Add label highlights.
* Code completion for labels in jumps. Provide a list if existing labels from which one can choose.
* Colored tooltips. Syntax highlighting in quickinfo tooltips.
* Code formatting.
* Syntax highlighting for the Debug/Windows/Disassembly view.
* Code completion restrictions. E.g. opcode movss can only be followed by an xmm register and not by a GPR such as rax.
* Track flag influence. Select a opcode that uses a flag (as input), find the opcodes that produce this flag (as output). E.g.  select opcode cmovc or setc , highlight all opcodes such as btr, sal, sar, shl, shr, etc.
* Register rename assistance. Highly desirable but very challenging. E.g. rename GPR rdx to rbx, find which rdx, edx, dx, dl and dh will need to be renamed, check if renames will clash with existing occurances of rbx, ebx, bx, bl and bh.
 
###Updates:
* 19 February 2016: Initial alpha release. Basic highlighting and descriptions for i368 instructions are available.
* 20 February 2016: Added highlighting and descriptions for SSE, AVX, AVX2 instructions.
* 21 February 2016: Added .vsix installer
* 22 February 2016: Added .vsix installer to the visual studio extensions gallery
* 23 February 2016: Added code completion
* 26 February 2016: Added code folding
* 2 March 2016: Added options for customizations.


