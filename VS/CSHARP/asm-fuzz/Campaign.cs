using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AsmFuzz;

/// <summary>
/// In-process, dependency-free fuzzing campaign so the fuzzer can be run — and crashes debugged —
/// straight from Visual Studio with F5 (no <c>sharpfuzz</c> instrumentation or <c>libfuzzer-dotnet</c>
/// needed).
/// <para>
/// IMPORTANT: this is DUMB (black-box) mutational fuzzing — it has NO coverage feedback, so unlike the
/// coverage-guided libFuzzer flow (the <c>fuzz-all</c> command) it cannot snowball its way into deep
/// multi-step states; giving it more time mostly explores breadth, not depth. It is meant for
/// quick from-IDE smoke campaigns, shallow-bug hunting and crash reproduction/triage — it complements,
/// not replaces, libFuzzer. Any exception escaping a target's <c>Run</c> is recorded as a crash
/// (targets already swallow their own EXPECTED exceptions internally).
/// </para>
/// </summary>
internal static class Campaign
{
    private static volatile bool _stop;

    /// <summary>
    /// <c>campaign &lt;target|all|t1,t2,…&gt; [--seconds N] [--seed N] [--corpus DIR] [--out DIR]
    /// [--stop-on-first] [--loop]</c>
    /// </summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: asm-fuzz campaign <target|all|t1,t2,...> [--seconds N] [--seed N] [--corpus DIR] [--out DIR] [--stop-on-first] [--loop]");
            return 2;
        }

        int seconds = 30;
        int seed = Environment.TickCount;
        string? corpusOverride = null;
        string? outOverride = null;
        bool stopOnFirst = false;
        bool loop = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seconds": seconds = int.Parse(args[++i]); break;
                case "--seed": seed = int.Parse(args[++i]); break;
                case "--corpus": corpusOverride = args[++i]; break;
                case "--out": outOverride = args[++i]; break;
                case "--stop-on-first": stopOnFirst = true; break;
                case "--loop": loop = true; break;
                default:
                    Console.Error.WriteLine($"unknown option: {args[i]}");
                    return 2;
            }
        }

        // Resolve the target list (single name, comma-list, or "all" = round-robin every target).
        string spec = args[0].ToLowerInvariant();
        List<string> targets;
        if (spec == "all")
        {
            targets = [.. FuzzTargets.All.Keys];
        }
        else
        {
            targets = [.. spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            foreach (string t in targets)
            {
                if (!FuzzTargets.All.ContainsKey(t))
                {
                    Console.Error.WriteLine($"Unknown target: {t}");
                    return 2;
                }
            }
        }

        if (corpusOverride != null && targets.Count != 1)
        {
            Console.Error.WriteLine("--corpus is only valid with a single target");
            return 2;
        }

        string projectDir = LocateProjectDir();
        var dict = LoadDictionary(Path.Combine(projectDir, "asm.dict"));

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // let us shut down cleanly instead of killing the process
            _stop = true;
            Console.WriteLine();
            Console.WriteLine("(stopping — Ctrl+C)");
        };

        Console.WriteLine($"campaign: {(spec == "all" ? "all targets" : string.Join(", ", targets))}");
        Console.WriteLine($"  {seconds}s/target, seed={seed}, dictionary={dict.Count} tokens{(loop ? ", looping" : "")}");
        Console.WriteLine();

        var rng = new Random(seed);
        int totalCrashes = 0;
        int pass = 0;

        do
        {
            pass++;
            if (loop)
            {
                Console.WriteLine($"=== pass {pass} ===");
            }

            foreach (string target in targets)
            {
                if (_stop)
                {
                    break;
                }

                string corpusDir = corpusOverride ?? Path.Combine(projectDir, "corpus", target);
                string outDir = outOverride ?? Path.Combine(projectDir, "crashes", target);
                totalCrashes += RunOne(target, FuzzTargets.All[target], corpusDir, outDir, dict, rng, seconds, stopOnFirst);

                if (stopOnFirst && totalCrashes > 0)
                {
                    _stop = true;
                    break;
                }
            }
        }
        while (loop && !_stop);

        Console.WriteLine();
        Console.WriteLine($"campaign finished: {totalCrashes} unique crash(es) total.");
        return totalCrashes > 0 ? 1 : 0;
    }

    /// <summary><c>replay &lt;target&gt; &lt;file&gt;</c> — run one input with NO exception handling, so the VS debugger breaks on the throwing line.</summary>
    public static int Replay(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: asm-fuzz replay <target> <file>");
            return 2;
        }

        string target = args[0].ToLowerInvariant();
        if (!FuzzTargets.All.TryGetValue(target, out var run))
        {
            Console.Error.WriteLine($"Unknown target: {target}");
            return 2;
        }

        byte[] input = File.ReadAllBytes(args[1]);
        Console.WriteLine($"replay: target={target} file={args[1]} ({input.Length} bytes)");
        run(input); // intentionally not guarded — break here under the debugger
        Console.WriteLine("replay: target returned without throwing.");
        return 0;
    }

    private static int RunOne(string target, FuzzTarget run, string corpusDir, string outDir, List<byte[]> dict, Random rng, int seconds, bool stopOnFirst)
    {
        List<byte[]> seeds = LoadSeeds(corpusDir);
        // Dedup by crash SIGNATURE (exception type + throwing site), not by input, so one bug hit by
        // many inputs counts/prints once and we save a single representative input per distinct bug.
        var seenSignatures = new HashSet<string>();
        var sw = Stopwatch.StartNew();
        var deadline = TimeSpan.FromSeconds(seconds);
        var lastReport = TimeSpan.Zero;
        long execs = 0;

        Console.WriteLine($"[{target}] {seeds.Count} seeds, budget {seconds}s");

        while (!_stop && sw.Elapsed < deadline)
        {
            byte[] input = Mutate(seeds, dict, rng);
            execs++;

            try
            {
                run(input);
            }
            catch (Exception ex)
            {
                if (seenSignatures.Add(Signature(ex)))
                {
                    string hash = SaveCrash(outDir, input, ex);
                    Console.WriteLine($"\r[{target}] CRASH #{seenSignatures.Count}: {ex.GetType().Name}: {Truncate(ex.Message, 90)}  -> crash-{hash[..12]}.bin");
                }

                if (stopOnFirst)
                {
                    break;
                }
            }

            if (sw.Elapsed - lastReport >= TimeSpan.FromSeconds(1))
            {
                lastReport = sw.Elapsed;
                double eps = execs / sw.Elapsed.TotalSeconds;
                Console.Write($"\r[{target}] {execs,12:n0} execs  {eps,9:n0}/s  {seenSignatures.Count} unique crash(es)   ");
            }
        }

        Console.Write($"\r[{target}] {execs,12:n0} execs  {execs / Math.Max(sw.Elapsed.TotalSeconds, 0.001),9:n0}/s  {seenSignatures.Count} unique crash(es)   ");
        Console.WriteLine();
        return seenSignatures.Count;
    }

    private static byte[] Mutate(List<byte[]> seeds, List<byte[]> dict, Random rng)
    {
        var buf = new List<byte>(seeds[rng.Next(seeds.Count)]);
        int rounds = 1 + rng.Next(4);

        for (int r = 0; r < rounds; r++)
        {
            int ops = dict.Count > 0 ? 7 : 6;
            switch (rng.Next(ops))
            {
                case 0: // flip a bit
                    if (buf.Count > 0)
                    {
                        int i = rng.Next(buf.Count);
                        buf[i] ^= (byte)(1 << rng.Next(8));
                    }
                    break;
                case 1: // set a random byte
                    if (buf.Count > 0)
                    {
                        buf[rng.Next(buf.Count)] = (byte)rng.Next(256);
                    }
                    break;
                case 2: // insert a random byte
                    buf.Insert(buf.Count == 0 ? 0 : rng.Next(buf.Count + 1), (byte)rng.Next(256));
                    break;
                case 3: // delete a byte
                    if (buf.Count > 0)
                    {
                        buf.RemoveAt(rng.Next(buf.Count));
                    }
                    break;
                case 4: // duplicate a chunk
                    if (buf.Count > 0)
                    {
                        int start = rng.Next(buf.Count);
                        int len = Math.Min(buf.Count - start, 1 + rng.Next(16));
                        List<byte> chunk = buf.GetRange(start, len);
                        buf.InsertRange(rng.Next(buf.Count + 1), chunk);
                    }
                    break;
                case 5: // splice in a prefix of another seed
                    {
                        byte[] other = seeds[rng.Next(seeds.Count)];
                        if (other.Length > 0)
                        {
                            int take = rng.Next(other.Length + 1);
                            buf.InsertRange(buf.Count == 0 ? 0 : rng.Next(buf.Count + 1), other.AsSpan(0, take).ToArray());
                        }
                    }
                    break;
                case 6: // insert a dictionary token
                    buf.InsertRange(buf.Count == 0 ? 0 : rng.Next(buf.Count + 1), dict[rng.Next(dict.Count)]);
                    break;
            }
        }

        if (buf.Count > FuzzLimits.MaxInputLength)
        {
            buf.RemoveRange(FuzzLimits.MaxInputLength, buf.Count - FuzzLimits.MaxInputLength);
        }

        return [.. buf];
    }

    /// <summary>A stable per-bug signature: exception type + the method that threw.</summary>
    private static string Signature(Exception ex)
    {
        string site = ex.TargetSite is { } m ? $"{m.DeclaringType?.FullName}.{m.Name}" : "?";
        return $"{ex.GetType().FullName}@{site}";
    }

    private static string SaveCrash(string outDir, byte[] input, Exception ex)
    {
        Directory.CreateDirectory(outDir);
        string hash = Convert.ToHexStringLower(SHA256.HashData(input));
        string stem = Path.Combine(outDir, $"crash-{hash[..12]}");
        File.WriteAllBytes(stem + ".bin", input);
        File.WriteAllText(stem + ".txt", $"{ex.GetType().FullName}: {ex.Message}\n\n{ex}\n");
        return hash;
    }

    private static List<byte[]> LoadSeeds(string corpusDir)
    {
        var seeds = new List<byte[]>();
        if (Directory.Exists(corpusDir))
        {
            foreach (string f in Directory.EnumerateFiles(corpusDir))
            {
                try
                {
                    seeds.Add(File.ReadAllBytes(f));
                }
                catch
                {
                    // ignore unreadable seed
                }
            }
        }

        if (seeds.Count == 0)
        {
            // Minimal fallbacks so the mutator has material to work with.
            seeds.Add([]);
            seeds.Add("mov rax, rbx\n"u8.ToArray());
            seeds.Add("{}"u8.ToArray());
        }

        return seeds;
    }

    /// <summary>Parses a libFuzzer-style dictionary (lines like <c>name="token"</c> or <c>"token"</c>).</summary>
    private static List<byte[]> LoadDictionary(string path)
    {
        var tokens = new List<byte[]>();
        if (!File.Exists(path))
        {
            return tokens;
        }

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            int first = line.IndexOf('"');
            int last = line.LastIndexOf('"');
            if (first < 0 || last <= first)
            {
                continue;
            }

            string body = line.Substring(first + 1, last - first - 1);
            var sb = new StringBuilder(body.Length);
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == '\\' && i + 1 < body.Length)
                {
                    i++; // minimal unescape: keep the escaped char literally (\" -> ", \\ -> \)
                }
                sb.Append(body[i]);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            if (bytes.Length > 0)
            {
                tokens.Add(bytes);
            }
        }

        return tokens;
    }

    /// <summary>Walks up from the executable to find the asm-fuzz project dir (the one holding <c>corpus/</c>).</summary>
    private static string LocateProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "asm-fuzz.csproj")) ||
                Directory.Exists(Path.Combine(dir.FullName, "corpus")))
            {
                return dir.FullName;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "…");
}
