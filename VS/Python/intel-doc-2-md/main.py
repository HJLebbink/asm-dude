# -*- coding: utf8 -*-

import sys
import os
import pdf2md

def main(argv):
	if len(argv) == 2:
		filename = argv[1]
		title = os.path.splitext(os.path.basename(filename))[0]
		print 'Parsing', filename
	else:
#		print 'usage:'
#		print '    python main.py <pdf>'
#		return
#		filename = 'H:/Dropbox/sc/ScHJ/Python/pdf-to-markdown/examples/resources/selection__(p14-15).pdf'
#		filename = 'H:/Dropbox/sc/ScHJ/Python/pdf-to-markdown/examples/resources/selection__(p250-253).pdf'
		filename = 'H:/Dropbox/sc/ScHJ/Python/pdf-to-markdown/examples/resources/selection.pdf'
		title = os.path.splitext(os.path.basename(filename))[0]
		print 'Parsing', filename


	parser = pdf2md.Parser(filename)
	parser.extract()
#	parser.extract(345, 354)
	piles = parser.parse()

	writer = pdf2md.Writer()
	writer.write(piles)


if __name__ == '__main__':
	main(sys.argv)

