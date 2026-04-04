#!/usr/bin/env python3
import subprocess
import json
import time
import sys


def send_message(writer, msg):
    content = json.dumps(msg, separators=(",", ":"))
    content_length = len(content.encode("utf-8"))
    header = "Content-Length: " + str(content_length) + "\r\n\r\n"
    writer.write((header + content).encode("utf-8"))
    writer.flush()


def read_message(reader, timeout=5.0):
    header = b""
    start_time = time.time()

    while not header.endswith(b"\r\n\r\n"):
        if time.time() - start_time > timeout:
            return None
        try:
            chunk = reader.read(1)
            if not chunk:
                return None
            header += chunk
        except:
            return None

    content_length = 0
    for line in header.decode("utf-8").split("\r\n"):
        if line.lower().startswith("content-length:"):
            try:
                content_length = int(line.split(":")[1].strip())
            except:
                return None
            break

    if content_length > 0:
        try:
            content = reader.read(content_length)
            return json.loads(content.decode("utf-8"))
        except:
            return None
    return None


def main():
    try:
        print("Starting AsmDude LSP server in stdio mode...")

        proc = subprocess.Popen(
            [
                "dotnet",
                "C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-ls/bin/Debug/net10.0-windows/AsmDude2.LSP.dll",
                "--stdio",
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )

        reader = proc.stdout
        writer = proc.stdin

        time.sleep(0.5)

        print("Sending initialize request...")
        init_request = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "processId": None,
                "roots": [],
                "capabilities": {
                    "textDocument": {"semanticTokens": {"requests": {"full": True}}}
                },
                "initializationOptions": {},
            },
        }

        send_message(writer, init_request)

        response = read_message(reader, timeout=5.0)
        print("\n=== Initialize Response ===")
        print(json.dumps(response, indent=2))

        print("\nSending initialized notification...")
        initialized = {"jsonrpc": "2.0", "method": "initialized", "params": {}}
        send_message(writer, initialized)

        print("\nSending textDocument/didOpen...")
        did_open = {
            "jsonrpc": "2.0",
            "method": "textDocument/didOpen",
            "params": {
                "textDocument": {
                    "uri": "file:///C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-vsix/Resources/examples/example_semantic_analysis.asm",
                    "languageId": "assembly",
                    "version": 1,
                    "text": open(
                        "C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-vsix/Resources/examples/example_semantic_analysis.asm",
                        "r",
                        encoding="utf-8",
                    ).read(),
                }
            },
        }

        send_message(writer, did_open)

        print("Waiting 2 seconds for document parsing...")
        time.sleep(2)

        print("\nChecking for any pending diagnostics notifications...")
        try:
            diagnostics = read_message(reader, timeout=1.0)
            if diagnostics:
                print("Diagnostics notification received:")
                print(json.dumps(diagnostics, indent=2))
        except:
            pass

        print("\nSending textDocument/semanticTokens/full request...")
        semantic_request = {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "textDocument/semanticTokens/full",
            "params": {
                "textDocument": {
                    "uri": "file:///C:/Source/Github/asm-dude/VS/CSHARP/asm-dude2-vsix/Resources/examples/example_semantic_analysis.asm"
                }
            },
        }

        send_message(writer, semantic_request)

        response = read_message(reader, timeout=5.0)
        print("\n=== Semantic Tokens Response ===")
        if response:
            print(json.dumps(response, indent=2))
        else:
            print("ERROR: No response received")

        if response and "result" in response:
            tokens = response["result"]["data"]
            num_tokens = len(tokens)
            print("\n=== Analysis ===")
            print("Total tokens returned:", num_tokens)

            token_types = {
                0: "keyword (mnemonics)",
                1: "variable (registers)",
                2: "label",
                3: "macro (directives)",
                4: "number",
                5: "operator (memory operands)",
                6: "comment",
                7: "string",
                8: "function",
                9: "decorator",
                10: "masmDirective",
                11: "nasmDirective",
                12: "masmOperator",
                13: "nasmOperator",
                14: "masmPseudoOp",
                15: "nasmPseudoOp",
            }

            print("\nToken type distribution:")
            type_counts = {}
            for i in range(0, num_tokens, 5):
                token_type = tokens[i + 2]
                type_counts[token_type] = type_counts.get(token_type, 0) + 1

            for token_type, count in sorted(type_counts.items()):
                type_name = token_types.get(
                    token_type, "unknown (" + str(token_type) + ")"
                )
                print("  Type " + str(token_type) + " (" + type_name + "):", count)

            print("\nMnemonic tokens (type 0):")
            count = 0
            for i in range(0, num_tokens, 5):
                if tokens[i + 2] == 0:
                    line = tokens[i]
                    char = tokens[i + 1]
                    length = tokens[i + 3]
                    print("  Line", line, "Char", char, "Length", length)
                    count += 1
                    if count >= 20:
                        print("  ...")
                        break

            print("\nRegister tokens (type 1):")
            count = 0
            for i in range(0, num_tokens, 5):
                if tokens[i + 2] == 1:
                    line = tokens[i]
                    char = tokens[i + 1]
                    length = tokens[i + 3]
                    print("  Line", line, "Char", char, "Length", length)
                    count += 1
                    if count >= 20:
                        print("  ...")
                        break

            print("\nLabel tokens (type 2):")
            count = 0
            for i in range(0, num_tokens, 5):
                if tokens[i + 2] == 2:
                    line = tokens[i]
                    char = tokens[i + 1]
                    length = tokens[i + 3]
                    print("  Line", line, "Char", char, "Length", length)
                    count += 1
                    if count >= 20:
                        print("  ...")
                        break

            print("\nChecking token encoding issues...")
            has_issues = False
            for i in range(0, num_tokens, 5):
                line = tokens[i]
                char = tokens[i + 1]
                token_type = tokens[i + 2]
                length = tokens[i + 3]
                modifiers = tokens[i + 4]

                if line < 0:
                    print("  ERROR: Negative line number at token", i // 5)
                    has_issues = True
                if char < 0:
                    print("  ERROR: Negative char at token", i // 5)
                    has_issues = True
                if length <= 0:
                    print("  ERROR: Invalid length at token", i // 5)
                    has_issues = True
                if token_type < 0 or token_type > 15:
                    print(
                        "  WARNING: Invalid token type", token_type, "at token", i // 5
                    )
                    has_issues = True
                if modifiers < 0:
                    print("  ERROR: Negative modifiers at token", i // 5)
                    has_issues = True

            if not has_issues:
                print("  No encoding issues found")

            print("\nToken encoding check complete.")
        elif response and "error" in response:
            print("\nERROR:", response["error"])

        print("\n=== Done ===")
        proc.terminate()
    except Exception as e:
        print("\nEXCEPTION:", e)
        import traceback

        traceback.print_exc()


if __name__ == "__main__":
    main()
