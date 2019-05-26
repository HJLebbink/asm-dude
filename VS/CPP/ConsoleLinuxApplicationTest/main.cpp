// 'Hello World!' program 

#include <iostream>
//#include <x86intrin.h>
#include <cstdio>

// 1] set a breakpoint at line 16.
// 2] hit Local Windows Debugger to run this program, 
// 3] the breakpoint will hit,
// 4] Press Control + Alt + D to open the Disassembly window

int main()
{
	//__m512d zmm_a = _mm512_set1_pd(1.0);
	//for (int i = 0; i < (1 << 6); ++i) {
	//	zmm_a = _mm512_div_pd(zmm_a, _mm512_set1_pd(i)); // search for vdivpd in the disassembly window
	//}

//	std::cout << "Hello world! " << zmm_a[0] << std::endl; // print the result such that it is not optimized away
	std::cout << "Press any key to exit" << std::endl;

	static_cast<void>(getchar());
	return 0;
}