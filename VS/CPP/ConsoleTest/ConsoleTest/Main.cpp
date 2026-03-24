/**
 * @brief Main entry point for AVX-512 SIMD performance test application.
 *
 * Executes iterative AVX-512 vector division operations using 512-bit ZMM registers.
 * Designed for debugging with the Visual Studio Disassembly window to verify
 * AVX-512 instruction generation (vdivpd) and ZMM register usage.
 *
 * @return Integer exit code (0 for success).
 *
 * @note This program is designed for interactive debugging with specific steps:
 *       1. Set breakpoint at line 16 (division operation inside loop)
 *       2. Run Local Windows Debugger (F5)
 *       3. Press Ctrl+Alt+D to open Disassembly window
 *       4. Search for "vdivpd" to verify AVX-512 instruction generation
 *       5. Verify ZMM register usage (zmm0-zmm31, 64-byte registers)
 *
 * @par Test scenarios:
 *      - Verify AVX-512 instruction generation (vdivpd)
 *      - Check ZMM register usage (512-bit = 64-byte registers)
 *      - Validate loop unrolling and SIMD packing
 *      - Confirm compiler.optimize SIMD code generation
 *
 * @par Compilation requirements:
 *      - Enable AVX-512 support: /arch:AVX512 (Visual Studio)
 *      - Include: <immintrin.h> (Intel intrinsic header)
 *      - Target: x64 platform (AVX-512 only available on 64-bit)
 *      - Compiler: Visual Studio 2017 or later with AVX-512 support
 *
 * @par Debug session example:
 * @code
 * // Debug → Start Debugging (F5)
 * // Breakpoint hits at line 16 (zmm_a = _mm512_div_pd(...))
 * // Debug → Windows → Disassembly
 * // Search: vdivpd
 * // Verify: Uses ZMM registers (zmm0-zmm31, 512-bit width)
 * // Expected: Instruction shows ZMM register operands (e.g., zmmword ptr)
 * @endcode
 *
 * @par Performance notes:
 *      - Loop executes 64 iterations (1 << 6)
 *      - Each iteration: 1 vdivpd instruction (512-bit, 8 doubles)
 *      - Total operations: 64 divisions of 8 doubles each = 512 divisions
 *      - Register width: ZMM (512-bit) = 8 × 64-bit doubles
 *
 * @warning Division by zero occurs when i=0, resulting in infinity values.
 *          The loop starts at i=0, so the first division divisor is 0.0.
 *
 * @author Henk-Jan Lebbink
 * @date 2026-03-21
 */
int main()
{
	// 512-bit ZMM register containing 8 double-precision values (AVX-512)
	// Broadcast scalar 1.0 to all 8 lanes of ZMM register
	__m512d zmm_a = _mm512_set1_pd(1.0);
	
	// AVX-512 SIMD division loop - 64 iterations (1 << 6)
	// Process 8 doubles per iteration using _mm512_div_pd (vdivpd instruction)
	// ZMM register width: 512 bits = 8 × 64-bit doubles
	for (int i = 0; i < (1 << 6); ++i) {
		// SIMD vector division intrinsic
		// Instruction: vdivpd (AVX-512)
		// Register: ZMM (512-bit, 8 doubles)
		// Operation: Divide each of 8 doubles in zmm_a by scalar 0.0 (when i=0)
		zmm_a = _mm512_div_pd(zmm_a, _mm512_set1_pd(i)); // search for vdivpd in the disassembly window
	}

	// Print first element to prevent compiler optimization from eliminating the computation
	// Access ZMM register element via union member m512d_f64[0]
	std::cout << "Hello world! " << zmm_a.m512d_f64[0] << std::endl; // print the result such that it is not optimized away
	std::cout << "Press any key to exit" << std::endl;

	// Wait for user input before exiting (prevents console window from closing)
	static_cast<void>(getchar());
	return 0;
}

/// LLM KEYWORDS: AVX-512, ZMM, SIMD, vdivpd, intrinsic, debugging, disassembly, 512-bit, 8 doubles
/// USED IN: ConsoleTest project, AVX-512 performance validation, SIMD instruction verification
/// SEE ALSO: ConsoleLinuxApplicationTest/main.cpp, _mm512_set1_pd, _mm512_div_pd, _mm256_set1_pd, _mm_set1_pd