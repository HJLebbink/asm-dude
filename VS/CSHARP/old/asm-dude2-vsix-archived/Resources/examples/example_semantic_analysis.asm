.intel_syntax noprefix

	#region Unreachable code
	#pragma assume mov al, 1 << 3
	cmp al, 1<<3
	jz label2
	mov al, 1
	jz label2
	mov al, 2
label2:
	mov rbx, rax
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region negative constants
	mov rax, -10
	mov rbx, 10
	add rax, rbx
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Jump test
	cmp al, 0
	jp label1
	mov al, 1
	jc label1
	mov al, 3
	jnz label1
	mov al, 2
label1:
	mov rbx, rax
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Semantic Error: usage of undefined register ax
	mov cl, bl
	xor cx, bx
	bsf ax, cx
	inc rax
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Test unimplemented instructions do not destroy known register content
	mov rax, 10
	subpd xmm1, xmm2
	mov rbx, rax # rbx has content 10
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator
	

	#region Semantic Error: usage of undefined carry 
	mov cl, 0
	bsf ax, cx
	add eax, 1
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator

	
	#region move value to memory
	mov ptr qword [rax], 10
	mov rax, ptr qword [rax]
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region slow (expensive) instruction
	mov ptr qword [rax], 10
	mov rax, ptr qword [rax]
	popcnt rbx, rax
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region moving undefined values to memory and retrieving it.
	mov cx, 0
	bsf ax, cx
	mov ptr dword [rbx], eax
	mov rcx, ptr qword [rbx]
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Redundant instruction warning
	mov rax, rbx
	mov rbx, rax
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Redundant instruction warning (but AsmSim may not find it simply because it times out)
	mov rax, rsp
	push rbx
	pop rcx
	mov rax, rsp
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Redundant instruction warning in memory (but AsmSim may not find it simply because it times out)
	mov rax, rbx
	mov qword [rcx], rax
	mov qword [rcx], rbx
	#endregion


	#pragma assume HLT ; HLT instruction will reset the simulator


	#region Redundant instruction warning in memory (but AsmSim may not find it simply because it times out)
	#mov qword [rdx], 0xFF #bug if memory content at rdx is known, then the redundant instruction is not flagged.
	mov rax, qword [rcx]
	mov rbx, qword [rdx]
	cmp rcx, rdx
	jne label4
	mov rbx, rax
	label4:


	#endregion




.att_syntax
