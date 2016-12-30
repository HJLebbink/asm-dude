.intel_syntax noprefix

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