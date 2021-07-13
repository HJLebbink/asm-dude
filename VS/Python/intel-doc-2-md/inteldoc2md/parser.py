

from pdfminer.pdfparser import PDFParser
from pdfminer.pdfdocument import PDFDocument
from pdfminer.pdfpage import PDFPage
from pdfminer.pdfinterp import PDFResourceManager
from pdfminer.pdfinterp import PDFPageInterpreter
from pdfminer.pdfdevice import PDFDevice
from pdfminer.layout import LAParams
from pdfminer.converter import PDFPageAggregator
from pile import Pile


class Parser(object):
	def __init__(self, filename):
		self._document = self._read_file(filename)
		self._device, self._interpreter = self._prepare_tools()
		self._pages = {}


	def extract(self, page_num_start=None, page_num_end=None):

		counter = 0
		page_counter = 1

		if (page_num_start == None):
			page_num_start = 0

		for page in PDFPage.create_pages(self._document):

			if (page_num_end != None) and (page_counter >= page_num_end):
				return

			if (page_counter >= page_num_start): 
				self._interpreter.process_page(page)
				layout = self._device.get_result()
				print('page no.' + str(page_counter) + '; extracted page no.' + str(layout.pageid))
				self._pages[counter] = layout
				counter = counter + 1

			page_counter = page_counter + 1


	def parse(self, page_num=None):
		piles = []
		if page_num == None:
			for page_num, page in self._pages.items():				
				piles += self._parse_page(page)
		else:
			page = self._pages[page_num]
			piles = self._parse_page(page)
		return piles


	def _read_file(self, filename):
		print('going to read file '+filename)
		parser = PDFParser(open(filename, 'rb'))
		document = PDFDocument(parser)
		print('done reading file '+filename)
		return document


	def _prepare_tools(self):
		laparams = LAParams()
		rsrcmgr = PDFResourceManager()
		device = PDFPageAggregator(rsrcmgr, laparams=laparams)
		interpreter = PDFPageInterpreter(rsrcmgr, device)
		return device, interpreter


	def _parse_page(self, page):
		print('parsing page '+str(page.pageid))
		pile = Pile()
		pile.parse_layout(page)
		piles = pile.split_piles()
		return piles

