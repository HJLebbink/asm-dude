.intel_syntax noprefix

# void bitswap_gas(unsigned int * const data, const unsigned long long pos1, const unsigned long long pos2) // rcx, rdx, r8, r9
# rcx <= data
# rdx <= pos1
# r8 <= pos2
# https://msdn.microsoft.com/en-us/library/9z1stfyw.aspx
# https://software.intel.com/sites/landingpage/IntrinsicsGuide/

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


#000000013F2CB440 4C 8B CA             mov         r9,rdx  
#000000013F2CB443 4D 8B D0             mov         r10,r8  
#000000013F2CB446 48 C1 EA 05          shr         rdx,5  
#000000013F2CB44A 49 C1 E8 05          shr         r8,5  
#000000013F2CB44E 49 83 E1 1F          and         r9,1Fh  
#000000013F2CB452 49 83 E2 1F          and         r10,1Fh  
#000000013F2CB456 44 0F B3 0C 91       btr         dword ptr [rcx+rdx*4],r9d  
#000000013F2CB45B 73 07                jae         bitswap_asm+24h (013F2CB464h)  
#000000013F2CB45D 46 0F AB 14 81       bts         dword ptr [rcx+r8*4],r10d  
#000000013F2CB462 EB 05                jmp         bitswap_asm+29h (013F2CB469h)  
#000000013F2CB464 46 0F B3 14 81       btr         dword ptr [rcx+r8*4],r10d  
#000000013F2CB469 73 05                jae         bitswap_asm+30h (013F2CB470h)  
#000000013F2CB46B 44 0F AB 0C 91       bts         dword ptr [rcx+rdx*4],r9d  
#000000013F2CB470 C3                   ret  

