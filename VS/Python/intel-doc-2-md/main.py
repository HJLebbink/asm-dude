
import sys
import os
import inteldoc2md

def main(argv):
	if len(argv) == 2:
		filename = argv[1]
		title = os.path.splitext(os.path.basename(filename))[0]
		print('Parsing', filename)
	else:
#		filename = './resources/test/jcc.pdf' # parse instruction ADD
#		filename = './resources/test/selection__(p14-15).pdf' # parse instruction ADD
#		filename = './resources/test/selection__(p250-253).pdf' # parse instruction CVTTPD2DQ
#		filename = './resources/325462-sdm-vol-1-2abcd-3abcd-selection.pdf'
#		filename = './resources/architecture-instruction-set-extensions-programming-reference-selection.pdf'
		filename = './resources/selection-ext.pdf'
		title = os.path.splitext(os.path.basename(filename))[0]
		print('Parsing', filename)


	parser = inteldoc2md.Parser(filename)
	parser.extract()
#	parser.extract(469, 473) # extract a selected range of pages
	piles = parser.parse()

	writer = inteldoc2md.Writer()
	writer.write(piles)


if __name__ == '__main__':
	main(sys.argv)

