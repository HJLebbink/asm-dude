

global_label1:
	xor rcx, rcx



procedure1 PROC a, b 
	jmp local_label1
	local_label1:

procedure1 ENDP

procedure2 PROC
	call procedure1
	invoke procedure1
	jmp local_label1

	local_label1: 
	jmp global_label1

procedure2 ENDP
