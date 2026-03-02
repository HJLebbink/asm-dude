; Basic x86 assembly with various instruction types
section .data
    msg db "Hello, World!", 0
    count dd 42
    buffer times 64 db 0

section .text
global _start

#region Main Entry Point
_start:
    mov rax, 1
    mov rdi, 1
    lea rsi, [msg]
    mov rdx, 13
    syscall

    ; Arithmetic
    add eax, ebx
    sub ecx, 0x10
    imul rdx, rcx, 8
    xor eax, eax

    ; SSE/AVX
    movaps xmm0, [buffer]
    addps xmm1, xmm0
    vmovdqa ymm0, [buffer]
    vaddps ymm1, ymm0, ymm1

    ; Jumps and calls
    cmp eax, 0
    je .done
    jmp .loop
    call my_func

.loop:
    dec ecx
    jnz .loop

.done:
    ret
#endregion

#region Helper Function
my_func PROC
    push rbp
    mov rbp, rsp
    mov eax, [rbp+8]
    pop rbp
    ret
my_func ENDP
#endregion
