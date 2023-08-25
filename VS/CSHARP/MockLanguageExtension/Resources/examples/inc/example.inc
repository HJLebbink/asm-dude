extrn printf : proc
include bla2

FOO: #label definition in included file
	xor rax, rax 
alias labelx = FOO

procedure3 PROC a, b 
	jmp local_label1
	local_label1:
	call printf

procedure3 ENDP
