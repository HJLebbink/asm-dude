       .text
LC0:
        .ascii "Hello, world!\12\0"
.globl _main
_main:
        pushl   %ebp
        movl    %esp, %ebp
        subl    $8, %esp
        andl    $-16, %esp
        movl    $0, %eax
        movl    %eax, -4(%ebp)
        movl    -4(%ebp), %eax
        call    __alloca
        call    ___main
        movl    $LC0, (%esp)
        call    _printf
        movl    $0, %eax
        leave
        ret


_static_initialization_and_destruction_0(int, int):
 push   %rbp 
 mov    %rsp,%rbp 
 sub    $0x10,%rsp 
 mov    %edi,-0x4(%rbp) 
 mov    %esi,-0x8(%rbp) 
 cmpl   $0x1,-0x4(%rbp) 
 jne    0x8000d1c <__static_initialization_and_destruction_0(int, int)+70> 
 cmpl   $0xffff,-0x8(%rbp) 
 jne    0x8000d1c <__static_initialization_and_destruction_0(int, int)+70> 
 lea    0x201437(%rip),%rdi        # 0x8202131 <_ZStL8__ioinit> 
 callq  0x8000880 <_ZNSt8ios_base4InitC1Ev@plt> 
 lea    0x201302(%rip),%rdx        # 0x8202008 
 lea    0x201424(%rip),%rsi        # 0x8202131 <_ZStL8__ioinit> 
 mov    0x2012e4(%rip),%rax        # 0x8201ff8 
 mov    %rax,%rdi 
 callq  0x8000830 <__cxa_atexit@plt> 
 nop 
 leaveq  
 retq    

