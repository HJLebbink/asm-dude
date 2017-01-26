;#region 
extrn printf : proc
;#endregion

procedure1 PROTO a, b 

global_label1:
	xor rcx, rcx

procedure1A PROC a, b 
	jmp local_label1
	local_label1:
	call printf

procedure1A ENDP

procedure2 PROC
	call procedure1A
	invoke procedure1
	jmp local_label1

	local_label1: 
	jmp global_label1

procedure2 ENDP
