<b>AAA</b> — ASCII Adjust for Add
<table>
	<tr>
		<td><b>Opcode</b></td>
		<td><b>Instruction</b></td>
		<td><b>Op/En</b></td>
		<td><b>64-Bit Mode</b></td>
		<td><b>Compat/Leg Mode</b></td>
		<td><b>Description</b></td>
	</tr>
	<tr>
		<td>37</td>
		<td>AAA</td>
		<td>NP</td>
		<td>Valid</td>
		<td>Valid</td>
		<td>ASCII Adjust for Add</td>
	</tr>
</table>

### Description

Adjusts the sum of two unpacked BCD values to create an unpacked BCD result. The AL register is the implied 
source and destination operand for this instruction. The AAA instruction is only useful when it follows an ADD 
instruction that adds (binary addition) two unpacked BCD values and stores a byte result in the AL register. The 
AAA instruction then adjusts the contents of the AL register to contain the correct 1-digit unpacked BCD result. 
If the addition produces a decimal carry, the AH register increments by 1, and the CF and AF flags are set. If there 
was no decimal carry, the CF and AF flags are cleared and the AH register is unchanged. In either case, bits 4 
through 7 of the AL register are set to 0.
This instruction executes as described in compatibility mode and legacy mode. It is not valid in 64-bit mode.

### Flags Affected

The AF and CF flags are set to 1 if the adjustment results in a decimal carry; otherwise they are set to 0. The OF, 
SF, ZF, and PF flags are undefined.
Protected Mode

