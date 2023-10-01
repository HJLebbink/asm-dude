.intel_syntax noprefix

include "inc\example.inc"
include "bla"

	jmp			FOO		# FOO is defined in an included file
	call procedure3

; give warning for an endregion that does not have an accompanying begin region
#endregion


    ; code completion suggestions when there exists another mnemonic that is a substring
	VPAND ymm0, y, 
    VPANDN

    KSHIFTLB K2, K2,  

	rep movs #rep movs
	repe cmps
	rep stos
	rep SCAS

#region 1 bit full adder
; ymm0 = A
; ymm1 = B
; ymm2 = Cin
; ymm5 = Sum
; ymm7 = Cout

pxor ymm3, ymm0, ymm1
pand ymm4, ymm0, ymm1
pand ymm6, ymm3, ymm2
pxor ymm5, ymm3, ymm2
por ymm7, ymm6, ymm4
#endregion

#region half adder
; ymm0 = A
; ymm1 = B
; ymm2 = Sum
; ymm3 = Cout
pxor ymm2, ymm0, ymm1
pand ymm3, ymm0, ymm1
#endregion
call

#region Things TODO
#######################################################
	mov			r13, QWORD PTR lennyOptions$[rsp]
	mov 		rsi, QWORD PTR [network_c_mp_network_neurons]
	vmovups		xmm4, XMMWORD PTR [.T1737_.0.17+64] # label not recognized
	vmovdqu		XMMWORD PTR [imagerel(htm_v1_constants_mp_global_cell_is_active_curr)+r13+r8], xmm15 

	lea			rcx, OFFSET FLAT:??_C@_0FE@OJFGMKFJ@ERROR?3?5dataset?3?3HexDataSet?3?3getV@
	mov			rax, -4616189618054758400		; negative constants not recognized bff0000000000000H

#endregion Things TODO

#region AVX-512 examples
#######################################################

	VALIGNQ zmm0 {k1}, zmm1, zmm2, 5 ; ok
	VALIGNQ zmm0, zmm1, zword [rax], 5			; packed 512-bit memory
	VALIGNQ zmm0, zmm1, qword [rax]{1to8}, 5 	; double-precision float broadcasted


	VDIVPS xmm4, xmm5, oword [rbx] 			; packed 128-bit memory
	VDIVPS ymm4, ymm5, yword [rbx] 			; packed 256-bit memory
	VDIVPS zmm4, zmm5, zword [rbx]          ; packed 512-bit memory

	VDIVPS xmm4, xmm5, dword [rbx]{1to4} 	; single-precision float broadcasted
	VDIVPS ymm4, ymm5, dword [rbx]{1to8} 	; single-precision float broadcasted
	VDIVPS zmm4, zmm5, dword [rbx]{1to16}   ; single-precision float broadcasted

	;VCVTPH2PS zmm1 {k1}{z}, ymm2/m256 {sae}
	;
	; Convert sixteen packed half precision (16-bit) floating-point values in ymm2/m256 to packed
	; single-precision floating-point values in zmm1.

	VCVTPH2PS zmm0{k1}, ymm1
	;VCVTPH2PS zmm0, ymm1 {0}
	vpconflictq zmm0{k7}{z},zmm1

	vcvttss2usi r8,xmm30 
	vcvttss2usi rax,xmm1,{sae} 
 
	vcvtss2usi rax,xmm30
	vcvtss2usi rax,xmm30,{rn-sae}
	vcvtss2usi rax,xmm30,{ru-sae}
	vcvtss2usi rax,xmm30,{rd-sae}
	vcvtss2usi rax,xmm30,{rz-sae} 
#endregion

#region VCVTPS2UDQ
#######################################################
	;VCVTPS2UDQ zmm1 {k1}{z}, zmm2/m512/m32bcst{er}
	;Convert sixteen packed single-precision floating-point
	;values from zmm2/m512/m32bcst to sixteen packed
	;unsigned doubleword values in zmm1 subject to
	;writemask k1.
	vcvtps2udq zmm0,zmm1
	vcvtps2udq zmm0,zmm1,{rz-sae}
	vcvtps2udq xmm0,xmm1
	vcvtps2udq zmm0{k7},zmm1
	vcvtps2udq zmm0{k7}{z},zmm1
	vcvtps2udq zmm0,zmm1,{rn-sae}
	vcvtps2udq zmm0,zmm1,{ru-sae}
	vcvtps2udq zmm0,zmm1,{rd-sae}
	vcvtps2udq zmm0,zmm1,{rz-sae}
	vcvtps2udq zmm0,ZWORD [rcx]
	vcvtps2udq zmm0,DWORD [rcx]{1to16}
	;vcvtps2udq zmm0,ZWORD [rcx],{rz-sae} ;error: Embedded rounding is available only with reg-reg op.
	vpscatterdd  [r14+zmm31*8+0x7b]{k1},zmm30
#endregion

#region Jump Examples
#######################################################
	jmp			$LL9@run.cpu$om
	jmp			SHORT $LL9@run.cpu$om
	jmp			$
	jmp			NEAR ptr $
	jmp			SHORT $+2
	jmp			rax

	??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z:
	call		??$?6U?$char_traits@D@std@@@std@@YAAEAV?$basic_ostream@DU?$char_traits@D@std@@@0@AEAV10@PEBD@Z
#endregion

#region Label Clash Example
#######################################################
$LL9@run.cpu$om: # multiple label definitions
$LL9@run.cpu$om: xor rax, rax
#endregion

#region All Registers
#######################################################
	rax 
	Rax
	RAX
	rax1
    EAX 
    AX 
    AL 
    AH 

    RBX 
    EBX 
    BX 
    BL 
    BH 

    RCX 
    ECX 
    CX 
    CL 
    CH 

    RDX 
    EDX 
    DX 
    DL 
    DH 

    RSI 
    ESI 
    SI 
    SIL 

    RDI 
    EDI 
    DI 
    DIL 

    RBP 
    EBP 
    BP 
    BPL 

    RSP 
    ESP 
    SP 
    SPL 

    R8 
    R8D 
    R8W 
    R8B 

    R9 
    R9D 
    R9W 
    R9B 

    R10 
    R10D 
    R10W 
    R10B 

    R11 
    R11D 
    R11W 
    R11B 

    R12 
    R12D 
    R12W 
    R12B 

    R13 
    R13D 
    R13W 
    R13B 

    R14 
    R14D 
    R14W 
    R14B 

    R15 
    R15D 
    R15W 
    R15B

    MM0 
    MM1 
    MM2 
    MM3 
    MM4 
    MM5 
    MM6 
    MM7 

    XMM0 
    XMM1 
    XMM2 
    XMM3 
    XMM4 
    XMM5 
    XMM6 
    XMM7 

    XMM8 
    XMM9 
    XMM10 
    XMM11 
    XMM12 
    XMM13 
    XMM14 
    XMM15 

    YMM0 
    YMM1 
    YMM2 
    YMM3 
    YMM4 
    YMM5 
    YMM6 
    YMM7 

    YMM8 
    YMM9 
    YMM10 
    YMM11 
    YMM12 
    YMM13 
    YMM14 
    YMM15 

    ZMM0 
    ZMM1 
    ZMM2 
    ZMM3 
    ZMM4 
    ZMM5 
    ZMM6 
    ZMM7 

    ZMM8 
    ZMM9 
    ZMM10 
    ZMM11 
    ZMM12 
    ZMM13 
    ZMM14 
    ZMM15 

    ZMM16 
    ZMM17 
    ZMM18 
    ZMM19 
    ZMM20 
    ZMM21 
    ZMM22 
    ZMM23 

    ZMM24 
    ZMM25 
    ZMM26 
    ZMM27 
    ZMM28 
    ZMM29 
    ZMM30 
    ZMM31 
	#endregion

#region Comment examples
	# Singleline comment

	# Multiline comment 1a
	# Multiline comment 2a

	# Multiline comment 1b
	# Multiline comment 2b
	# Multiline comment 3b
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
#endregion

.att_syntax
