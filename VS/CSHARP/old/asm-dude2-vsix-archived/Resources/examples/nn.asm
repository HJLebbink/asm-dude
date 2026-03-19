.intel_syntax noprefix


.align 16
.data
# 	__m256i maskA = _mm256_set_epi8(0, 1, 4, 5, 8, 9, 12, 13, 16, 17, 20, 21, 24, 25, 28, 29);
maskA:
    .long   0x0C0D0E0F
    .long   0x08090A0B
    .long   0x04050607
    .long   0x00010203

    .long   0x0E0F
    .long   0x0A0B0C0D
    .long   0x08090405
    .long   0x00010405

.align 64
.text                                           # Code section
.global nn_gas
nn_gas:

;	constants
	mov eax, 32
	vmovd XMM6, eax
	VBROADCASTSS  ZMM6, XMM6

;	parameters
	mov r10, 10 ; r10 (const) = previous states ptr
	mov r11, 11 ; r11 (const) = new states ptr
	mov r12, 32 ; r12 (const) = number of neurons, has to be multiple of 16
	mov r13, 13 ; r13 (const) = weights ptr

;	code	
	
	mov r15, r12	; r15 (const) = number of times loop1 and loop2 has to be taken
	shl r15, 5		; divide by 16

	mov r9, r15		; init loop2 counter; r9 = loop2 counter
	mov rbx, r13	; init weights ptr; rbx = weight ptr index
	mov rcx, r11	; init new state ptr; rcx = previouis state ptr index

	#region Loop2 code
	loop2:			; loop2 calculates the new state of all neurons: r9 = loop2 counter

	mov rdx, r10	; init previous state ptr
	mov rax, r15	; init loop1 counter; rax is loop1 counter
	
	vxorpd zmm0, zmm0	; zmm0 is the new state; set new state of 16 neurons to zero

	#region Loop1 code
	loop1:	; loop calculates new state of 16 neurons
	vmovdqa32 zmm10, zmmword ptr [rbx + (0 * 64)]
	vmovdqa32 zmm11, zmmword ptr [rbx + (1 * 64)]
	vmovdqa32 zmm12, zmmword ptr [rbx + (2 * 64)]
	vmovdqa32 zmm13, zmmword ptr [rbx + (3 * 64)]
	vmovdqa32 zmm14, zmmword ptr [rbx + (4 * 64)]
	vmovdqa32 zmm15, zmmword ptr [rbx + (5 * 64)]
	vmovdqa32 zmm16, zmmword ptr [rbx + (6 * 64)]
	vmovdqa32 zmm17, zmmword ptr [rbx + (7 * 64)]

	vp4dpwssds zmm0, zmm10, xmmword ptr [rdx + (0 * 64)]
	vp4dpwssds zmm0, zmm14, xmmword ptr [rdx + (1 * 64)]
	
	add rdx, 2*64	; update previous state ptr
	add rbx, 8*64	; update weights ptr
	dec rax			; decrement loop1 counter
	jnz loop1
	#endregion Loop1 code

	#region Do something with the new state, eq threshold it
	;zmm0 contains the new state of 16 neurons but these states are i32 and have to be reduced to i16
	vcvtdq2ps zmm0, zmm0	; convert i32 to Single-Precision FP

	vmovd xmm1, r12d
	vbroadcastss zmm2, xmm1
	vdivps zmm0, zmm0, zmm2 ; divide by the number of neurons

	vcvtps2dq zmm0, zmm0	; convert Single-Precision FP to i32
	vpxor zmm1, zmm1
	vmovdqa32 ymm1, ymmword ptr [maskA]
	vpermb zmm0, zmm1, zmm0

	vmovdqa32 ymmword ptr [rcx], ymm0	; store the new states
	#endregion

	add rcx, 32		; update new state ptr
	dec r9			; decrement loop2 counter
	jnz loop2
	#endregion Loop2 code

	ret

	jmp loop1

	label1:
	label1:

	label2:
	label1:




.att_syntax