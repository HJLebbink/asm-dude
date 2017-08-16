# -*- coding: utf8 -*-

import os
import re
import pdb
import time

class State(object):
	def __init__(self):
		self.code_mode = False;
		self.type = None
		self.type_next = None
	
class Writer(object):

	def __init__(self):
		self.source = 'Intel® Architecture Instruction Set Extensions Programming Reference (APRIL 2017)'
		#self.source = 'Intel® Architecture Software Developer\'s Manual (JULY 2017)'

	def close_file(self, instruction, markdown):
		filename = './output/' + str(instruction).replace('/', '_').replace(' ', '_') + '.md'
		print 'writing ' + filename
		fwrite = open(filename, 'w')
		markdown += '\n --- \n'
		markdown += '<p align="right"><i>Source: '+self.source+'<br>Generated at: '+time.strftime("%c")+'</i></p>\n'
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
		state.code_mode = False;
		state.type = None
		state.type_next = None

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
				
			if (createNewFile):
				self.close_file(instruction_prev, markdown)
				markdown = ''

			markdown += pile.gen_markdown(state)

		self.close_file(instruction_curr, markdown)
