.intel_syntax noprefix

	#region Semantic Error: usage of undefined register ax
	mov cl, bl
	xor cx, bx
	bsf ax, cx
	inc rax
	#endregion

	vaddpd xmm1, xmm2, xmm3 ; inimplemented instruction to stop the simulator
	

	#region Semantic Error: usage of undefined carry 
	mov cl, 0
	bsf ax, cx
	add eax, 1
	#endregion

	vaddpd xmm1, xmm2, xmm3 ; inimplemented instruction to stop the simulator
	
	#region move value to memory
	mov ptr qword [rax], 10
	mov rax, ptr qword [rax]
	#endregion

	vaddpd xmm1, xmm2, xmm3 ; inimplemented instruction to stop the simulator

	#region slow (expensive) instruction
	mov ptr qword [rax], 10
	mov rax, ptr qword [rax]
	popcnt rbx, rax
	#endregion

	vaddpd xmm1, xmm2, xmm3 ; inimplemented instruction to stop the simulator

	#region moving undefined values to memory and retrieving it.
	mov cx, 0
	bsf ax, cx
	mov ptr dword [rbx], eax
	mov rcx, ptr qword [rbx]
	#endregion

	;vaddpd xmm1, xmm2, xmm3 ; inimplemented instruction to stop the simulator

	#region Redundant instruction warning
	mov rax, rbx
	mov rbx, rax
	#endregion

	#region Redundant instruction warning in memory
	mov qword [rcx], rax  
	mov qword [rcx], rbx
	#endregion


.att_syntax
