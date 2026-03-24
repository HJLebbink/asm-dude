/**
 * @brief Main entry point for SSE2/SSE3 SIMD performance test application.
 *
 * Executes iterative SSE vector division operations using 128-bit XMM registers.
 * This is a subset test compared to ConsoleTest/Main.cpp, using older SSE intrinsics
 * instead of AVX-512. Designed for Linux/WSL debugging with gdb.
 *
 * @return Integer exit code (0 for success).
 *
 * @note This program is designed for interactive debugging:
 *       1. Set breakpoint at line 29 (division operation inside loop)
 *       2. Run Local Windows Debugger (F5) with WSL integration
 *       3. Press Ctrl+Alt+D to open Disassembly window
 *       4. Search for "divpd" to verify SSE instruction generation
 *
 * @par Test scenarios:
 *      - Verify SSE instruction generation (divpd)
 *      - Check XMM register usage (128-bit = 16-byte registers)
 *      - Validate backward compatibility with older SIMD instruction sets
 *      - Compare performance with AVX-512 (ConsoleTest/Main.cpp)
 *
 * @par Compilation requirements:
 *      - Include: <x86intrin.h> (Cross-platform Intel intrinsic header)
 *      - Target: x64 or x86 platform (SSE available on both)
 *      - Compiler: GCC (Linux/WSL) or Visual Studio
 *
 * @par WSL setup command:
 * @code
 * sudo apt install g++ gdb make rsync zip
 * @endcode
 *
 * @par Debug session example:
 * @code
 * // Debug → Start Debugging (F5) with WSL
 * // Breakpoint hits at line 29 (xmm_a = _mm_div_pd(...))
 * // Debug → Windows → Disassembly
 * // Search: divpd
 * // Verify: Uses XMM registers (xmm0-xmm15, 128-bit width)
 * // Expected: Instruction shows XMM register operands (e.g., xmmword ptr)
 * @endcode
 *
 * @par Performance notes:
 *      - Loop executes 16 iterations (1 << 4)
 *      - Each iteration: 1 divpd instruction (128-bit, 2 doubles)
 *      - Total operations: 16 divisions of 2 doubles each = 32 divisions
 *      - Register width: XMM (128-bit) = 2 × 64-bit doubles
 *      - Note: _mm_div_pd may map to different instructions based on CPU
 *              (divpd on older SSE, divpd/vdivpd on AVX, vdivpd on AVX-512)
 *
 * @warning Division by zero occurs when i=0, resulting in infinity values.
 *          The loop starts at i=0, so the first division divisor is 0.0.
 *
 * @par Comparison with AVX-512:
 *      - SSE (this file): 128-bit XMM, 2 doubles per operation, 16 iterations
 *      - AVX (commented): 256-bit YMM, 4 doubles per operation, 32 iterations
 *      - AVX-512 (ConsoleTest): 512-bit ZMM, 8 doubles per operation, 64 iterations
 *      - Performance scaling: ZMM > YMM > XMM for same iteration count
 *
 * @author Henk-Jan Lebbink
 * @date 2026-03-21
 */
int main()
{
	// 512-bit ZMM register commented out - AVX-512 version (see ConsoleTest/Main.cpp)
	// __m512d zmm_a = _mm512_set1_pd(1.0);
	// for (int i = 0; i < (1 << 6); ++i) {
	//     zmm_a = _mm512_div_pd(zmm_a, _mm512_set1_pd(i));
	// }

	// 256-bit YMM register commented out - AVX version (see also ConsoleTest/Main.cpp)
	// __m256d ymm_a = _mm256_set1_pd(1.0);
	// for (int i = 0; i < (1 << 5); ++i) {
	//     ymm_a = _mm256_div_pd(ymm_a, _mm256_set1_pd(i));
	// }

	// 128-bit XMM register containing 2 double-precision values (SSE2/SSE3)
	// Broadcast scalar 1.0 to both lanes of XMM register
	// Register width: 128 bits = 2 × 64-bit doubles
	__m128d xmm_a = _mm_set1_pd(1.0);
	
	// SSE SIMD division loop - 16 iterations (1 << 4)
	// Process 2 doubles per iteration using _mm_div_pd
	// XMM register width: 128 bits = 2 × 64-bit doubles
	// Note: _mm_div_pd may map to divpd (SSE), vdivpd (AVX), or vdivpd (AVX-512)
	for (int i = 0; i < (1 << 4); ++i) {
		// SIMD vector division intrinsic
		// Instruction: divpd (SSE2) or vdivpd (AVX/AVX-512)
		// Register: XMM (128-bit, 2 doubles)
		// Operation: Divide each of 2 doubles in xmm_a by scalar 0.0 (when i=0)
		xmm_a = _mm_div_pd(xmm_a, _mm_set1_pd(i)); // search for vdivpd in the disassembly window
	}
	
	// Print first element to prevent compiler optimization from eliminating the computation
	// Access XMM register element via array subscript operator
	std::cout << "Hello world! " << xmm_a[0] << std::endl; // print the result such that it is not optimized away

	std::cout << "Press any key to exit" << std::endl;

	// Wait for user input before exiting (prevents console window from closing)
	static_cast<void>(getchar());
	return 0;
}

/// LLM KEYWORDS: SSE, XMM, SIMD, divpd, intrinsic, debugging, disassembly, 128-bit, 2 doubles, Linux, WSL
/// USED IN: ConsoleLinuxApplicationTest project, SSE performance validation, backward compatibility test
/// SEE ALSO: ConsoleTest/Main.cpp, _mm_set1_pd, _mm_div_pd, _mm256_set1_pd, _mm512_set1_pd