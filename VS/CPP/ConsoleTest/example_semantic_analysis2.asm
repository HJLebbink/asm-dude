	;mov rax, 10
	cmp rax, 0
	jz label1
	xor rax, rax
	mov rcx, rax
	add rax, 1
label1:
	inc rax
	mov rbx, rax
	mov rax, rbx



	mov rcx, 10
labelx:
	dec rcx
	jnz labelx
	mov rax, rcx

	aaa 

	mov rcx, 10
	mov r10, rcx ; store the parameter rcx in r10 
;mod3    PROC
; parameter 1: rcx
    mov       r8, 0aaaaaaaaaaaaaaabH      ;; (scaled) reciprocal of 3
    mov       rax, rcx
    mul       r8                          ;; multiply with reciprocal
    shr       rdx, 1                      ;; quotient
    lea       r9, QWORD PTR [rdx+rdx*2]   ;; back multiply with 3
    neg       r9
    add       rcx, r9                     ;; subtract from dividend 
    ;mov       rax, rcx                    ;; remainder
	; rcx has the result (mod3)

;   ret
;mod3    ENDP


	mov       rax, r10
	xor       rdx, rdx
	mov		  r8, 3
	idiv      r8
	; rdx has the result (mod3)

