; Edge cases: empty lines, weird spacing, nested regions

#region Outer
#region Inner
    nop
#endregion
#endregion

    mov eax,ebx
    mov eax ,  ebx
    mov eax, [rax+rbx*4+0x100]
    mov DWORD PTR [rsp-8], 0

; Duplicate labels
dup_label:
    nop
dup_label:
    nop

; Very long line
    add eax, ebx ; this is a comment that goes on and on and on and on and on and on and on and on

; Empty operand list
    ret
    nop
    hlt

; Forward reference
    jmp forward_target
    nop
forward_target:
    ret

; Directives mixed with instructions
.data
    db 0xFF, 0xFE, 0xFD
.code
INCLUDE nonexistent.inc
    mov eax, 1
