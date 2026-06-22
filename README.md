# AsmDude3 - Modern Assembly Language Support for Visual Studio

Welcome to **AsmDude3**, the next-generation assembly language extension for Visual Studio 2022 and later.

## 📚 Documentation

**Start Here** ⭐:
- **[QUICK_START.md](QUICK_START.md)** - Get AsmDude3 running in 5 minutes (installation & first use)
- **[FEATURES.md](FEATURES.md)** - Complete feature matrix and capabilities

**For Users**:
- **[USER_GUIDE.md](USER_GUIDE.md)** - Comprehensive user manual with all features
- **[CONFIGURATION.md](CONFIGURATION.md)** - Customize colors, assemblers, architectures, and settings
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Solutions for common problems

**For Developers**:
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - System design and implementation details (coming soon)
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - How to contribute to the project (coming soon)

---

## About AsmDude3

AsmDude3 represents a natural evolution from its predecessor, AsmDude. While AsmDude served as a
single, all-encompassing plugin for VS2015/17/19, providing support for Assembly source code,
AsmDude3 is built around a Language Server Protocol ([LSP](https://microsoft.github.io/language-server-protocol/))
and a lightweight Visual Studio extension (for VS 2022 and later), drawing its functionality from this LSP.

**Key Features**:
- ✅ **Syntax highlighting** for MASM, NASM Intel, and NASM AT&T
- ✅ **56+ CPU architectures** (x86/x64, SSE, AVX, AVX-512, and more)
- ✅ **Clickable hyperlinks** in hover tooltips to online documentation
- ✅ **Performance data** showing latency, throughput, and µOps for multiple microarchitectures
- ✅ **Code folding** for procedures and sections
- ✅ **Auto-detection** of assembler syntax
- ✅ **157 customizable settings** for colors, architectures, and performance
- ✅ **Disassembly support** for debugger output

This extension can be found in the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=Henk-JanLebbink.AsmDude3)
or download from [GitHub releases](https://github.com/HJLebbink/asm-dude/releases). 

### Features

#### Syntax highlighting and Code Folding
AsmDude2 offers support for the following architectures: the instruction sets of x86 and x64, as well as 
SSE, AVX, AVX2, Xeon-Phi (Knights Corner), and AVX-512 instructions.
Most of the commonly used Masm directives covered, along with a selection of Nasm directives.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/main/Images/AsmDude2-syntax-highlighting.png?raw=true "Syntax highlighting")

#### Code Descriptions with Clickable Hyperlinks
When you hover over a mnemonic, you receive a pop-up with descriptions and **clickable hyperlinks** to online documentation.
Click on the instruction name to open detailed documentation in your browser.

The hover tooltip shows:
- **Instruction name** (clickable link to documentation)
- **Description** from curated signature files
- **Architecture support** (e.g., [X64, SSE, AVX])
- **Performance data** (latency, throughput, µOps) for Haswell, Skylake, and other microarchitectures

Descriptions can be modified by updating the AsmDudeData.xml file, located alongside the installed plugin binaries.

![code-descriptions](https://github.com/HJLebbink/asm-dude/blob/main/Images/AsmDude2-descriptions.png?raw=true "Code descriptions with clickable hyperlinks")

#### Code Completion 
While typing text, the completion lists will be refined to display the relevant 
language keywords. This applies to all keywords. However, please be aware that code suggestions 
may not be flawless at this stage; only valid code completions should be proposed.

![code-completion](https://github.com/HJLebbink/asm-dude/blob/main/Images/AsmDude2-code-completion.png?raw=true "Code Completion")

#### ⚠ "Silly" gray suggestions are GitHub Copilot, not AsmDude

If you see **gray, multi-line "ghost text" suggestions** in your `.asm` files that look like nonsensical
assembly, those come from **GitHub Copilot**, *not* from AsmDude. Copilot treats `.asm` as source code and
offers its own AI completions, which it is not good at for assembly. AsmDude cannot disable Copilot for you
— Copilot is a separate product with no per-language opt-out an extension can set, and the VS Code
`github.copilot.enable` JSON does **not** apply to Visual Studio. You can switch it off yourself in seconds:

**Turn off Copilot completions for assembly only (recommended):**
1. Open any `.asm` file.
2. Click the **GitHub Copilot icon** (status bar / upper-right of the Visual Studio window).
3. Choose **Disable Completions** → pick the **language-specific** option (disables Copilot for the active
   asm language only, leaving C#/C++/etc. untouched).

**Or via Options:** *Tools → Options → GitHub → Copilot → Copilot Completions*, and turn completions off
(globally, or use the language-specific control).

AsmDude's own completion list (mnemonics, registers, etc. — shown in the popup, not as gray ghost text)
is unaffected by this and keeps working.

#### Signature Help
Signature Help, also referred to as Parameter Info, presents the method's signature in a tooltip when
a user enters the character marking the start of the parameter list (e.g., in C++, an opening parenthesis). 
As the user types a parameter and a parameter separator (usually a comma), 
the tooltip is refreshed to display the next parameter in bold.

![label-analysis](https://github.com/HJLebbink/asm-dude/blob/main/Images/AsmDude2-signature-help.png?raw=true "Signature Help")

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

## 🛠 For Developers

AsmDude3 is built on .NET 10 and uses:
- Language Server Protocol (LSP)
- Roslyn scripting for expression evaluation
- Z3 SMT solver for symbolic simulation
- Modern C# 12/13 features (span parsing, file-scoped namespaces, etc.)

See [`CODE_STYLE.md`](CODE_STYLE.md) for coding conventions and style guidelines.

### Code Maintenance Policy

**Commented-out code**: Commented-out code is allowed and encouraged to be kept in the codebase. You (the developer) are responsible for manually cleaning up such code when appropriate — this is not something the AI assistant will do.
