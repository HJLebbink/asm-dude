// 'Hello World!' program 

#include <iostream>
#include <intrin.h>

// hit Local Windows Debugger to run this program, 
// the breakpoint will hit,
// Press Control + Alt + D to open the Disassembly window

int main()
{

	__m512d zmm_a = _mm512_set1_pd(1.0);
	for (int i = 0; i < (1 << 6); ++i) {
		zmm_a = _mm512_div_pd(zmm_a, _mm512_set1_pd(i)); // search for vdivpd in the disassembly window
	}

	std::cout << "Hello world! " << zmm_a.m512d_f64[0] << std::endl;
	std::cout << "Press any key to exit" << std::endl;

	int c = getchar();
	return 0;
}