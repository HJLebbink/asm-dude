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