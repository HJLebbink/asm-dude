#extract pages
pdftk A=325462-sdm-vol-1-2abcd-3abcd.pdf cat A590-1125 A1134-1827 A1833-2453 A2461-2496 output selection.pdf

#pdftk A=../selection.pdf cat A1-49 output selection-1.pdf 
#pdftk A=../selection.pdf cat A50-200 output selection-2.pdf
#pdftk A=../selection.pdf cat A201-400 output selection-3.pdf
