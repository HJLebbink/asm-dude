
import os
import re
import pdb
import datetime

class State(object):
	def __init__(self):
		self.code_mode = False;
		self.type = None
		self.type_next = None
	
class Writer(object):

	def __init__(self):
		self.source = 'Intel® Architecture Instruction Set Extensions and Future Features Programming Reference (December 2020)'
		#self.source = 'Intel® Architecture Software Developer\'s Manual (May 2018)'


	@staticmethod
	def _cleanup_hyphens(str):

		str = str.replace('•\n\n', '\n * ')
		str = str.replace('•\n', '\n * ')
		str = str.replace('•', '\n * ')

		str = str.replace('addi-\ntional', 'additional\n')

		str = str.replace('combina-\ntion ', 'combination\n')
		str = str.replace('compar-\nison)', 'comparison)\n')
		str = str.replace('compar-\nisons', 'comparisons\n')
		str = str.replace('corre-\nsponding', 'corresponding\n')

		 
		str = str.replace('documenta-\ntion', 'documentation\n')
		str = str.replace('destina-\ntion', 'destination\n')
		str = str.replace('desti-\nnation', 'destination\n')
		str = str.replace('infor-\nmation', 'information\n')
		str = str.replace('instruc-\ntions', 'instructions\n')
		str = str.replace('instruc-\ntion', 'instruction\n')
		str = str.replace('regis-\nters', 'registers\n')
		str = str.replace('regis-\nter', 'register\n')
		str = str.replace('oper-\nands', 'operands\n') 
		str = str.replace('oper-\nations', 'operations\n')
		
		str = str.replace('preci-\nsion', 'precision\n')
		str = str.replace('loca-\ntions', 'locations\n')
		str = str.replace('loca-\ntion', 'location\n')
		str = str.replace('speci-\nfied', 'specified\n')
		str = str.replace('64-\nbit', '64-bit\n')
		str = str.replace('unpre-\ndictable', '\nunpredictable')
		str = str.replace('single-\nprecision', '\nsingle-precision')
		str = str.replace('priv-\nilege', '\nprivilege')
		
		
		str = str.replace('single- precision', 'single-precision')
		str = str.replace('no- operand', 'no-operand')
		str = str.replace('no- operands', 'no-operands')
		str = str.replace('REP/REPE/REPZ /REPNE/REPNZ', 'REP/REPE/REPZ/REPNE/REPNZ')
		str = str.replace('general- purpose', 'general-purpose')
		str = str.replace('general- protection', 'general-protection')
		str = str.replace('excep- tion', 'exception')
		
		return str


	@staticmethod
	def _find_prev_opcode_table(i, piles, instruction_curr):
		for j in range(i-1, 0, -1):
			pile = piles[j]
			pileInstruction, descr = pile._get_instruction()
			if ((pileInstruction != None) and (pileInstruction != instruction_curr)):
				return False
			if (pile._is_table()):
				return pile._is_opcode_table()
		return False

	@staticmethod
	def _find_next_opcode_table(i, piles, instruction_curr):
		for j in range(i+1, len(piles)):
			pile = piles[j]
			pileInstruction, descr = pile._get_instruction()
			if ((pileInstruction != None) and (pileInstruction != instruction_curr)):
				return False
			if (pile._is_table()):
				return pile._is_opcode_table()
		return False


	def close_file(self, instruction, markdown):

		markdown = Writer._cleanup_hyphens(markdown)

		filename = './output/' + str(instruction).replace('/', '_').replace(' ', '_') + '.md'
		print('writing ' + filename)
		fwrite = open(filename, 'w')

		now = datetime.datetime.now()
		generatedTime = str(now.day) + '-' + str(now.month) + '-' + str(now.year)
		#generatedTime = '24-10-2017'
		markdown += '\n --- \n<p align="right"><i>Source: '+self.source+'<br>Generated: '+generatedTime+'</i></p>\n'
		fwrite.write(markdown)
		fwrite.close()


	def write(self, piles):
		createNewFile = False

		if (len(piles) > 0):
			instruction_curr, descr = piles[0]._get_instruction()
		else:
			instruction_curr = None
			descr = None

		instruction_prev = instruction_curr

		state = State()
		state.code_mode = False
		state.type = None
		state.type_next = None

		state.prev_pile_is_opcode_table = False
		state.curr_pile_is_opcode_table = False
		state.next_pile_is_opcode_table = False

		markdown = ''

		for i in range (0, len(piles)):
			pile = piles[i]
			pileInstruction, descr = pile._get_instruction()
			#print 'pileInstruction ' + str(pileInstruction)

			createNewFile = False
			if (pileInstruction != None):
				if (pileInstruction != instruction_curr):
					createNewFile = True
					instruction_prev = instruction_curr
					instruction_curr = pileInstruction
					#print 'instruction_prev=' + str(instruction_prev) +'; instruction_curr='+instruction_curr
			
			state.curr_pile_is_opcode_table = pile._is_opcode_table()
			if (state.curr_pile_is_opcode_table):
				state.prev_pile_is_opcode_table = Writer._find_prev_opcode_table(i, piles, instruction_curr)
				state.next_pile_is_opcode_table = Writer._find_next_opcode_table(i, piles, instruction_curr)
			else:
				state.prev_pile_is_opcode_table = False
				state.next_pile_is_opcode_table = False

			#print 'write: ', pile.texts[0].get_text().encode('utf8').strip()
			#print 'write: ', state.prev_pile_is_opcode_table,' ',  state.curr_pile_is_opcode_table, ' ',  state.next_pile_is_opcode_table

			if (createNewFile):
				self.close_file(instruction_prev, markdown)
				markdown = ''
				state.prev_pile_is_opcode_table = False

			markdown += pile.gen_markdown(state)

		self.close_file(instruction_curr, markdown)
