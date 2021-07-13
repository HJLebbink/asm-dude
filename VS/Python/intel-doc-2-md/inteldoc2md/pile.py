
from pdfminer.layout import LTFigure
from pdfminer.layout import LTTextBox
from pdfminer.layout import LTTextLine
from pdfminer.layout import LTTextBoxHorizontal
from pdfminer.layout import LTTextLineHorizontal
from pdfminer.layout import LTLine
from pdfminer.layout import LTRect
from pdfminer.layout import LTImage
from pdfminer.layout import LTCurve
from pdfminer.layout import LTChar
from pdfminer.layout import LTLine
from pdfminer.layout import LTAnno
import binascii
import re
from operator import itemgetter, attrgetter


class MyPile(object):
	def __init__(self):
		self.verticals = []
		self.horizontals = []
		self.texts = []
		self.images = []

		self._SEARCH_DISTANCE_VERTICAL = 1.0
		self._SEARCH_DISTANCE_HORIZONTAL = 8.0

	def __nonzero__(self):
		return bool(self.texts)


	def get_type(self):
		if self.verticals:
			return 'table'
		elif self.images:
			return 'image'
		else:
			return 'paragraph'


	def parse_layout(self, layout):
		obj_stack = list(reversed(list(layout)))
		while obj_stack:
			obj = obj_stack.pop()
			#print 'Pile:parse_layout: type: ' + str(type(obj))

			if type(obj) in [LTFigure, LTTextBox, LTTextLine, LTTextBoxHorizontal]:
				#print 'Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip()
				obj_stack.extend(reversed(list(obj)))
			elif type(obj) == LTTextLineHorizontal:
				#print 'Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip()
				self.texts.append(obj)
			elif type(obj) == LTRect:
				if obj.width < 1.0:
					self._adjust_to_close(obj, self.verticals, 'x0', self._SEARCH_DISTANCE_VERTICAL)
					self.verticals.append(obj)
				elif obj.height < 1.0:
					self._adjust_to_close(obj, self.horizontals, 'y0', self._SEARCH_DISTANCE_HORIZONTAL)
					self.horizontals.append(obj)
			elif type(obj) == LTImage:
				print('Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip())
				self.images.append(obj)
			elif type(obj) == LTCurve:
				#print 'Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip()
				pass
			elif type(obj) == LTChar:
				#print 'Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip()
				pass
			elif type(obj) == LTLine:
				#print 'Pile:parse_layout: type='+str(type(obj))+'; content = '+ obj.get_text().encode('utf8').strip()
				pass
			else:
				print('Pile:parse_layout: Unrecognized type: ' + str(type(obj)))

	@staticmethod
	def get_key(x):
		anything = x._get_anything()
		if anything != None:
			return anything.y0
		else:
			return None


	def split_piles(self):
		tables = self._find_tables()
		paragraphs = self._find_paragraphs(tables)
		images = self._find_images()

		piles = sorted(tables + paragraphs + images, reverse=True, key=Pile.get_key)
		return piles


	def gen_markdown(self, state):
		pile_type = self.get_type()
		if pile_type == 'paragraph':
			return self._gen_paragraph_markdown(state)
		elif pile_type == 'table':
			return self._gen_table_markdown(state)
		else:
			raise Exception('Unsupported markdown type')


	def get_image(self):
		if not self.images:
			raise Exception('No images here')
		return self.images[0]


	def _adjust_to_close(self, obj, lines, attr, search_distance):
		obj_coor = getattr(obj, attr)
		close = None
		for line in lines:
			line_coor = getattr(line, attr)
			if abs(obj_coor - line_coor) < search_distance:
				close = line
				break

		if not close:
			return

		if attr == 'x0':
			new_bbox = (close.bbox[0], obj.bbox[1], close.bbox[2], obj.bbox[3])
		elif attr == 'y0':
			new_bbox = (obj.bbox[0], close.bbox[1], obj.bbox[2], close.bbox[3])
		else:
			raise Exception('No such attr')
		obj.set_bbox(new_bbox)


	def _find_tables(self):
		tables = []
		visited = set()
		for vertical in self.verticals:
			if vertical in visited:
				continue

			near_verticals = self._find_near_verticals(vertical, self.verticals)
			top, bottom = self._calc_top_bottom(near_verticals)
			included_horizontals = self._find_included(top, bottom, self.horizontals)
			included_texts = self._find_included(top, bottom, self.texts)

			table = Pile()
			table.verticals = near_verticals
			table.horizontals = included_horizontals
			table.texts = included_texts

			tables.append(table)
			visited.update(near_verticals)
		return tables


	def _find_paragraphs(self, tables):
		tops = []
		for table in tables:
			top, bottom = self._calc_top_bottom(table.verticals)
			tops.append(top)

		tops.append(float('-inf')) # for the last part of paragraph

		all_table_texts = set()
		for table in tables:
			all_table_texts.update(table.texts)

		num_slots = len(tables) + 1
		paragraphs = [Pile() for idx in range(num_slots)]
		for text in self.texts:
			content = text.get_text().encode('utf8').strip()
			if text in all_table_texts:
				continue
			for idx, top in enumerate(tops):
				if text.y0 > top:
					paragraphs[idx].texts.append(text)
					break

		paragraphs = filter(None, paragraphs)

		return paragraphs


	def _find_images(self):
		images = []
		for image in self.images:
			pile = Pile()
			pile.images.append(image)
			images.append(pile)
		return images


	def _get_anything(self):
		if self.texts:
			return self.texts[0]
		if self.images:
			return self.images[0]
		#print '_get_anything: The pile contains nothing'
		return None


	def _is_overlap(self, top, bottom, obj):
		search_distance = 0.7
		return (bottom - search_distance) <= obj.y0 <= (top + search_distance) or \
			   (bottom - search_distance) <= obj.y1 <= (top + search_distance)


	def _calc_top_bottom(self, objects):
		top = float('-inf')
		bottom = float('inf')
		for obj in objects:
			top = max(top, obj.y1)
			bottom = min(bottom, obj.y0)
		return top, bottom


	def _find_near_verticals(self, start, verticals):
		near_verticals = [start]
		top = start.y1
		bottom = start.y0
		for vertical in verticals:
			if vertical == start:
				continue
			if self._is_overlap(top, bottom, vertical):
				near_verticals.append(vertical)
				top, bottom = self._calc_top_bottom(near_verticals)
		return near_verticals


	def _find_included(self, top, bottom, objects):
		included = []
		for obj in objects:
			if self._is_overlap(top, bottom, obj):
				included.append(obj)
		return included


	def _get_instruction(self):
		for text in self.texts:
			fontname = text._objs[0].fontname
			#print '_get_instruction: fontname='+fontname +'; text.height='+str(text.height) +'; content='+text.get_text().encode('utf8').strip()
				
			if ((text.height > 14.5) and (fontname.endswith('NeoSansIntelMedium'))):
				content = text.get_text().encode('utf8').strip()
				#print '_get_instruction: text.height='+str(text.height) +'; content='+content
	
				searchChar = '—'
				#searchChar = '\xe2\x80\x94'
				if (re.search(searchChar, content)):
					tmp = content.split(searchChar)
					instruction = tmp[0].strip()
					descr = tmp[1]
					#print '_get_instruction: instruction='+instruction
					return instruction, descr
					
				searchChar = '–'
				if (re.search(searchChar, content)):
					tmp = content.split(searchChar)
					instruction = tmp[0].strip()
					descr = tmp[1]
					#print '_get_instruction: instruction='+instruction
					return instruction, descr
					
				continue

		return None, None


	@staticmethod
	def _create_indent(xpos):
		indent = None
		offset = 47
		w = 18
		if   (xpos < (offset + (0 * w))): return '    ' * 0
		elif (xpos < (offset + (1 * w))): return '    ' * 1
		elif (xpos < (offset + (2 * w))): return '    ' * 2
		elif (xpos < (offset + (3 * w))): return '    ' * 3
		elif (xpos < (offset + (4 * w))): return '    ' * 4
		elif (xpos < (offset + (5 * w))): return '    ' * 5
		elif (xpos < (offset + (6 * w))): return '    ' * 6
		return '    ' * 7

	@staticmethod
	def _close_code(state):
		result = '```' if (state.code_mode) else ''
		state.code_mode = False
		return result

	@staticmethod
	def _start_code(state, language):
		if (state.code_mode):
			return ''
		else:
			state.code_mode = True
			return '```'+language+'\n'

	@staticmethod
	def _par(height):
		return ('\n' if (height > 15) else '')

	@staticmethod
	def _header(text):
		return '\n### '+text+'\n'

	@staticmethod
	def mycmp(x, y):
		if (x.y1 < y.y1):
			return -1
		elif (x.y1 > y.y1):
			return 1
		else:
			if (x.x0 < y.x0):
				return 1
			elif (x.x0 > y.x0):
				return -1
			else:
				return 0

	def _gen_paragraph_markdown(self, state):
		markdown = ''
		previousHeight = 0
		counter = 0;

		for text in sorted(self.texts, cmp=Pile.mycmp, reverse=True):

			content2 = text.get_text().encode('utf8')
			#print 'content2='+content2
			
			if (counter == 0):
				if re.search('INSTRUCTION SET REFERENCE, ', content2):
					continue
				if re.search('SAFER MODE EXTENSIONS REFERENCE', content2):
					continue
				
			if re.search('Vol. 2', content2):
				#print('found "Vol. 2" with height '+str(text.y1))
				if (text.height < 10.0):
					continue
				else:
					#print('found "Vol. 2" in main text '+content2)
					pass

			if re.search('Ref. ', content2):
				#print('found "Ref. " with height '+str(text.y1))
				if (text.height < 10.0):
					continue
				else:
					#print('found "Ref. " in main text '+content2)
					pass

			if content2.startswith('5-'):
				#print('found "5-" with height '+str(text.y1))
				if (text.height < 10.0):
					continue
				else:
					#print('found "5 -" in main text '+content2)
					pass
	
			content = content2.strip().replace('#', '\#').replace('*', '\*')
			#print 'content='+content

				
			if re.search('\xe2\x80\x94', content):
				#print('found "\xe2\x80\x94" with height '+str(text.height))

				if (text.height < 10.0):
					#found a footer
					continue

				instruction, descr = self._get_instruction()
				if (instruction != None): 
					state.type_next = 'title'
					instruction = instruction.replace('/', ' / ')
					#markdown += '\n\n#' + ' ' +  instruction +'\n\n'
					markdown += '<b>'+instruction + '</b> \xe2\x80\x94 '  + descr + '\n'
					continue

			if (content == 'Description') or (content=='IA-32 Architecture Compatibility'):
				state.type_next = 'description'
				markdown += Pile._header(content)

			elif (content == 'Instruction Operand Encoding'):
				state.type_next = 'encoding'
				markdown += Pile._header(content)

			elif content == 'Operation':
				state.type_next = 'operation'
				markdown += Pile._header(content) + '\n'

			elif (content == 'Flags Affected') or (content == 'FPU Flags Affected'):
				state.type_next = 'flags'
				markdown += Pile._close_code(state) + Pile._header(content)

			elif (content == 'Intel C/C++ Compiler Intrinsic Equivalent') or (content == 'C/C++ Compiler Intrinsic Equivalent'):
				state.type_next = 'intrinsics'
				markdown += Pile._close_code(state) + Pile._header(content) + Pile._start_code(state, 'c')

			elif (content == 'Other Exceptions') or \
				 (content == 'Compatibility Mode Exceptions') or \
				 (content == '64-Bit Mode Exceptions') or \
				 (content == 'Exceptions (All Operating Modes)') or \
				 (content == 'Floating-Point Exceptions') or \
				 (content == 'Other Mode Exceptions') or \
				 (content == 'Virtual-8086 Mode Exceptions') or \
				 (content == 'SIMD Floating-Point Exceptions') or \
				 (content == 'SIMD Floating Point Exceptions') or \
				 (content == 'Protected Mode Exceptions') or \
				 (content == 'Exceptions') or \
				 (content == 'Numeric Exceptions') or \
				 (content == 'Virtual 8086 Mode Exceptions') or \
				 (content == 'Real-Address Mode Exceptions'):
				state.type_next = 'exceptions'
				markdown += Pile._close_code(state) + Pile._header(content)

			else:
				if state.type == 'title':
					markdown += content + '\n'

				elif state.type == 'description':
					myheight = previousHeight - text.y1;
					previousHeight = text.y1
					markdown += Pile._par(myheight) + content + '\n'

				elif state.type == 'encoding':
					#markdown += Pile._par(myheight) + content + '\n'
					pass

				elif state.type == 'operation': # code mode
					fontname = text._objs[0].fontname
				
					if (fontname.endswith('NeoSansIntelMedium')):
						markdown += Pile._close_code(state) + '\n#### '+content+'\n' + Pile._start_code(state, 'java')
					else:
						markdown += Pile._start_code(state, 'java') + Pile._create_indent(text.x0) + content2.replace('', '←')

				elif state.type == 'intrinsics': # code mode
					markdown += content2

				elif state.type == 'flags':
					myheight = previousHeight - text.y1;
					previousHeight = text.y1
					markdown += Pile._par(myheight) + content + '\n'

				elif state.type == 'exceptions':
					if re.search('\#', content):
						if re.search('\(\#', content): 
							print('Pile:_gen_paragraph_markdown: not changing "(#"')
							#pass
						else:
							content = content.replace('\#', '<p>#')


					myheight = previousHeight - text.y1;
					previousHeight = text.y1
					markdown += Pile._par(myheight) + content + '\n'


			state.type = state.type_next
			counter = counter+1
		return markdown


	def _gen_table_markdown(self, state):
		intermediate = self._gen_table_intermediate()
		return self._intermediate_to_markdown(intermediate, state)


	def _gen_table_intermediate(self):
		vertical_coor = self._calc_coordinates(self.verticals, 'x0', False)
		horizontal_coor = self._calc_coordinates(self.horizontals, 'y0', True)
		num_rows = len(horizontal_coor) - 1
		num_cols = len(vertical_coor) - 1

		intermediate = [[] for idx in range(num_rows)]
		for row_idx in range(num_rows):
			for col_idx in range(num_cols):
				left = vertical_coor[col_idx]
				top = horizontal_coor[row_idx]
				right = vertical_coor[col_idx + 1]
				bottom = horizontal_coor[row_idx + 1]

				if self._is_ignore_cell(left, top, right, bottom):
					#print 'Pile:_gen_table_intermediate: ignoring cell'
					continue

				right, colspan = self._find_exist_coor(bottom, top, col_idx, vertical_coor, 'vertical')
				bottom, rowspan = self._find_exist_coor(left, right, row_idx, horizontal_coor, 'horizontal')

				cell = {}
				cell['texts'] = self._find_cell_texts(left, top, right, bottom)
				if colspan > 1:
					cell['colspan'] = colspan
				if rowspan > 1:
					cell['rowspan'] = rowspan

				intermediate[row_idx].append(cell)

		return intermediate


	def _find_cell_texts(self, left, top, right, bottom):
		texts = []
		for text in self.texts:
			if self._in_range(left, top, right, bottom, text):
				content = text.get_text().encode('utf8').strip()
				#if re.search('\xe2\x89\xA0', content): # unequal sign
				#	print 'Pile:_find_cell_texts: in range: content='+content
				texts.append(text)
			else: 
				#content = text.get_text().encode('utf8').strip()
				#if re.search('\xe2\x89\xA0', content): # unequal sign
				#	print 'Pile:_find_cell_texts: not in range: content='+content
				pass
		return texts


	def _in_range(self, left, top, right, bottom, obj):

		if (obj.x0 >= obj.x1):
			print('Pile:_in_range: empty x')
			return False
		if (obj.y0 >= obj.y1): 
			print('Pile:_in_range: empty y')
			return False

		left_range = (left - self._SEARCH_DISTANCE_VERTICAL) <= obj.x0
		if (not left_range):
			return False

		if (False): # use old method
			right_range = obj.x1 <= (right + self._SEARCH_DISTANCE_VERTICAL)
			if (not right_range):
				return False
		else:
			right_range = obj.x0 <= right
			if (not right_range):
				return False

		top_range = obj.y1 <= (top + self._SEARCH_DISTANCE_HORIZONTAL)
		if (not top_range):
			return False

		bottom_range = (bottom - self._SEARCH_DISTANCE_HORIZONTAL) <= obj.y0
		if (not bottom_range):
			return False

		return True


	def _is_ignore_cell(self, left, top, right, bottom):
		left_exist = self._line_exists(left, bottom, top, 'vertical')
		top_exist = self._line_exists(top, left, right, 'horizontal')
		result = not left_exist or not top_exist
		#if (result):
		#	print 'Pile:_is_ignore_cell yields true for pile '
		return result


	def _find_exist_coor(self, minimum, maximum, start_idx, line_coor, direction):
		span = 0
		line_coor_len = len(line_coor)

		line_exist = False
		while not line_exist:
			span += 1

			if ((start_idx + span) < line_coor_len):
				coor = line_coor[start_idx + span]
				line_exist = self._line_exists(coor, minimum, maximum, direction)
			else:
				line_exist = True
		return coor, span


	def _line_exists(self, target, minimum, maximum, direction):
		if direction == 'vertical':
			lines = self.verticals
			attr = 'x0'
			fill_range = self._fill_vertical_range
		elif direction == 'horizontal':
			lines = self.horizontals
			attr = 'y0'
			fill_range = self._fill_horizontal_range
		else:
			raise Exception('No such direction')

		for line in lines:
			if getattr(line, attr) != target:
				continue
			if fill_range(minimum, maximum, line):
				return True

		return False


	def _fill_vertical_range(self, bottom, top, obj):
		return obj.y0 <= (bottom + self._SEARCH_DISTANCE_VERTICAL) and (top - self._SEARCH_DISTANCE_VERTICAL) <= obj.y1


	def _fill_horizontal_range(self, left, right, obj):
		return obj.x0 <= (left + self._SEARCH_DISTANCE_HORIZONTAL) and (right - self._SEARCH_DISTANCE_HORIZONTAL) <= obj.x1


	def _intermediate_to_markdown(self, intermediate, state):
		markdown = ''

		#print '_intermediate_to_markdown: prev ', state.prev_pile_is_opcode_table,'; curr ',  state.curr_pile_is_opcode_table, '; next ',  state.next_pile_is_opcode_table

		firstLine = True
		if (state.curr_pile_is_opcode_table and state.prev_pile_is_opcode_table):
			intermediate.pop(0)
			firstLine = False
		else:
			markdown += self._create_tag('table', True, 0)

		for row in intermediate:
			markdown += self._create_tag('tr', True, 1)
			for cell in row:
				markdown += self._create_td_tag(cell, firstLine)
			markdown += self._create_tag('tr', False, 1)
			firstLine = False

		if (state.curr_pile_is_opcode_table and state.next_pile_is_opcode_table):
			pass
		else:
			markdown += self._create_tag('table', False, 0)
			markdown += '\n'

		return markdown


	def _create_tag(self, tag_name, start, level):
		indent = '\t' * level
		slash = '' if start else '/'
		return indent + '<' + slash + tag_name + '>\n'


	def _create_td_tag(self, cell, firstLine):
		indent = '\t' * 2
		texts = [text.get_text().encode('utf8').strip() for text in cell['texts']]
		texts = ' '.join(texts)

		#print '_create_td_tag: texts='+texts

#		texts = texts.replace('≠', '!=')
		colspan = ' colspan={}'.format(cell['colspan']) if 'colspan' in cell else ''
		rowspan = ' rowspan={}'.format(cell['rowspan']) if 'rowspan' in cell else ''
		if firstLine:
		    return indent + '<td' + colspan + rowspan + '><b>' + texts + '</b></td>\n'
		else:
		    return indent + '<td' + colspan + rowspan + '>' + texts + '</td>\n'


	def _calc_coordinates(self, axes, attr, reverse):
		coor_set = set()
		for axis in axes:
			coor_set.add(getattr(axis, attr))
		coor_list = list(coor_set)
		coor_list.sort(reverse=reverse)
		return coor_list


	def _is_table(self):
		return (self.get_type() == 'table')


	def _is_opcode_table(self):
		if (self._is_table()):
			if (len(self.texts) > 0):
				first_word = self.texts[0].get_text().encode('utf8').strip()
				#print '_is_opcode_table first_word ', first_word
				if (first_word == 'Opcode'):
					return True
		return False


