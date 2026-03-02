#!/bin/bash
# Coverage-guided fuzzing for AsmDude2 parser using SharpFuzz + libFuzzer
#
# Prerequisites:
#   dotnet tool install --global SharpFuzz.CommandLine
#   Download libfuzzer-dotnet from https://github.com/Metalnem/libfuzzer-dotnet/releases
#   Place libfuzzer-dotnet.exe on your PATH (or set LIBFUZZER_DOTNET below)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
BIN_DIR="$PROJECT_DIR/bin/Release/net10.0-windows"
LIBFUZZER_DOTNET="${LIBFUZZER_DOTNET:-libfuzzer-dotnet.exe}"

TARGET="${1:-}"
MAX_LEN="${MAX_LEN:-4096}"
JOBS="${JOBS:-1}"

if [ -z "$TARGET" ]; then
    echo "Usage: $0 <target> [libfuzzer-args...]"
    echo ""
    echo "Available targets:"
    echo "  parsememoperand    - AsmSourceTools.Parse_Mem_Operand (known bug)"
    echo "  parseline          - AsmSourceTools.ParseLine"
    echo "  evaluateconstant   - ExpressionEvaluator.Parse_Constant"
    echo "  splitintokeywords  - AsmSourceTools.SplitIntoKeywordsType"
    echo "  operand            - new Operand(input)"
    echo "  parsemnemonic      - AsmSourceTools.ParseMnemonic"
    echo "  documentpipeline   - Full LS document pipeline (open, hover, completion, etc.)"
    echo "  labelgraph         - LabelGraph construction and diagnostics"
    echo ""
    echo "Environment variables:"
    echo "  LIBFUZZER_DOTNET   Path to libfuzzer-dotnet.exe (default: on PATH)"
    echo "  MAX_LEN            Maximum input length (default: 4096)"
    echo "  JOBS               Number of parallel jobs (default: 1)"
    exit 1
fi

shift

echo "=== Building asm-fuzz (Release) ==="
dotnet build "$PROJECT_DIR/asm-fuzz.csproj" -c Release -v quiet

echo "=== Instrumenting asm-tools-lib.dll ==="
sharpfuzz "$BIN_DIR/asm-tools-lib.dll"

# LS targets also need asm-dude2-ls-lib instrumented
LS_TARGETS="documentpipeline labelgraph"
if echo "$LS_TARGETS" | grep -qw "$TARGET"; then
    echo "=== Instrumenting asm-dude2-ls-lib.dll (LS target) ==="
    sharpfuzz "$BIN_DIR/asm-dude2-ls-lib.dll"
fi

CORPUS_DIR="$PROJECT_DIR/corpus/$TARGET"
mkdir -p "$CORPUS_DIR"

echo "=== Running fuzzer: $TARGET ==="
echo "    Corpus: $CORPUS_DIR"
echo "    Max length: $MAX_LEN"
echo "    Jobs: $JOBS"
echo ""

"$LIBFUZZER_DOTNET" \
    --target_path="$BIN_DIR/asm-fuzz.exe" \
    --target_arg="$TARGET" \
    -dict="$PROJECT_DIR/asm.dict" \
    -max_len="$MAX_LEN" \
    -jobs="$JOBS" \
    "$@" \
    "$CORPUS_DIR"
