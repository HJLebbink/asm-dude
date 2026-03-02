; Label graph seed: labels, PROC/ENDP, includes, forward/backward refs
_start:
    jmp end_label
    call my_proc
    jmp _start

my_proc PROC
    push rbp
    mov rbp, rsp
    call helper
    pop rbp
    ret
my_proc ENDP

helper PROC
    xor eax, eax
    ret
helper ENDP

; Duplicate labels
dup:
    nop
dup:
    nop

; Undefined reference
    jmp nowhere

end_label:
    ret

; Include directive
INCLUDE some_file.inc

; Case variations
MyLabel:
    jmp MYLABEL
    jmp mylabel
