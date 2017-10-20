// 'Hello World!' program 

#include <iostream>

// hit Local Windows Debugger to run this program, 
// the breakpoint will hit,
// Press Control + Alt + D to open the Disassembly window

int main()
{
	std::cout << "Hello World!" << std::endl;
	std::cout << "Press any key to exit" << std::endl;
	int c = getchar();
	return 0;
}