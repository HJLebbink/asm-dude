.intel_syntax noprefix

include "inc\example.inc"
%include "bla"

#region Nasm has local labels when prefixed with a dot
#######################################################

global_label1:
.loop:
    jne     .loop
    ret 

	jmp dword ptr 


global_label2:
.loop:
    jne     .loop
	jae     global_label1.loop
    ret 

#endregion


struc mytype1
  mt_long:      resd    1 
  mt_word:      resw    1 
  mt_byte:      resb    1 
  mt_str:       resb    32 
endstruc

struc mytype2 
  mt_long:      resd    1 
  mt_word:      resw    1 
  mt_byte:      resb    1 
  mt_str:       resb    32 
endstruc

struc mytype3 
  .long:        resd    1 
  .word:        resw    1 
  .byte:        resb    1 
  .str:         resb    32 
endstruc

mystruc: 
    istruc mytype1 
        at mt_long, dd      123456 
        at mt_word, dw      1024 
        at mt_byte, db      'x' 
        at mt_str,  db      'hello, world', 13, 10, 0 
    iend


#region Numeric Constants
#######################################################
	mov     ax,200          ; decimal 
	mov     ax,0200         ; still decimal 
	mov     ax,0200d        ; explicitly decimal 
	mov     ax,0d200        ; also decimal 
	mov     ax,0c8h         ; hex 
	mov     ax,$0c8         ; hex again: the 0 is required 
	mov     ax,0xc8         ; hex yet again 
	mov     ax,0hc8         ; still hex 
	mov     ax,310q         ; octal 
	mov     ax,310o         ; octal again 
	mov     ax,0o310        ; octal yet again 
	mov     ax,0q310        ; octal yet again 
	mov     ax,11001000b    ; binary 
	mov     ax,1100_1000b   ; same binary constant 
	mov     ax,1100_1000y   ; same binary constant once more 
	mov     ax,0b1100_1000  ; same binary constant yet again 
	mov     ax,0y1100_1000  ; same binary constant yet again
#endregion

#region Floating-point Constants
#######################################################
	db    -0.2                    ; "Quarter precision" 
	dw    -0.5                    ; IEEE 754r/SSE5 half precision 
	dd    1.2                     ; an easy one 
	dd    1.222_222_222           ; underscores are permitted 
	dd    0x1p+2                  ; 1.0x2^2 = 4.0 
	dq    0x1p+32                 ; 1.0x2^32 = 4 294 967 296.0 
	dq    1.e10                   ; 10 000 000 000.0 
	dq    1.e+10                  ; synonymous with 1.e10 
	dq    1.e-10                  ; 0.000 000 000 1 
	dt    3.141592653589793238462 ; pi 
	do    1.e+4000                ; IEEE 754r quad precision

	mov    rax,__float64__(3.141592653589793238462)
	mov    rax,0x400921fb54442d18
#endregion


#region Nasm Examples
#######################################################

_str_wi:
    enter 16,0
    mov ebx,[ebp + 8]
    mov ecx,[ebp + 12]
    mov dl,[ebp + 16]
    cmp ecx,0
    je loop.end

loop:
    inc ebx
    loop loop
.end:
    mov [ebx],dl
    leave
    ret

_str_ri:
    enter 12,0
    mov si,[ebp + 8]
    mov ecx,[ebp + 12]
    loop .loop
.loop:
    lodsb

#endregion 

#region Real Example Handcoded
#######################################################
# void bitswap_gas(unsigned int * const data, const unsigned long long pos1, const unsigned long long pos2) // rcx, rdx, r8, r9
# rcx <= data
# rdx <= pos1
# r8 <= pos2

.text                                           # Code section
.global bitswap_gas
bitswap_gas:

	mov         r9,rdx
	shr         rdx,5
	and         r9,0x1F

	mov         r10,r8
	shr         r8,5
	and         r10,0x1F

	btr         dword [rcx+4*rdx],r9d
	jnc         label1

	bts         dword [rcx+4*r8],r10d
	jmp         label2
label1:
	btr         dword [rcx+4*r8],r10d
label2:
	jnc         label3
	bts         dword [rcx+4*rdx],r9d
label3:
	ret
.att_syntax
#endregion