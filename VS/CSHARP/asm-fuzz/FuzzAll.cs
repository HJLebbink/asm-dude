using System.ComponentModel;
using System.Diagnostics;

namespace AsmFuzz;

/// <summary>
/// Coverage-guided round-robin driver — the C# equivalent of a "fuzz-all" shell script. It
/// instruments the target DLLs once with <c>sharpfuzz</c>, then loops over every fuzz target running
/// <c>libfuzzer-dotnet</c> for a per-target time budget, with the on-disk corpus persisting between
/// slices so coverage progress accumulates across rounds (and passes).
/// <para>
/// Unlike the in-process <see cref="Campaign"/>, this IS coverage-guided (the strong, deep-state
/// search) — but it shells out to the external <c>sharpfuzz</c> global tool and <c>libfuzzer-dotnet</c>
/// driver, which must be installed. It only ever references <see cref="FuzzTargetNames"/> (strings), so
/// the orchestrator process never loads — and never file-locks — the DLLs it needs to instrument.
/// </para>
/// </summary>
internal static class FuzzAll
{
    private static volatile bool _stop;

    /// <summary>Default number of parallel libFuzzer worker processes per target (override with --workers).</summary>
    private const int DefaultWorkers = 8;

    /// <summary><c>fuzz-all [--seconds N] [--passes P] [--workers N] [--only t1,t2] [--bin DIR] [--libfuzzer PATH] [--skip-instrument] [--no-merge] [--merge-only]</c></summary>
    public static int Run(string[] args)
    {
        int seconds = 600;              // per-target slice
        int passes = 0;                 // 0 = loop until Ctrl+C
        int workers = DefaultWorkers;   // parallel libFuzzer processes per target (1 = single, live console)
        string? only = null;
        string? binOverride = null;
        string libfuzzer = "libfuzzer-dotnet.exe";
        bool skipInstrument = false;
        bool skipMerge = false;         // --no-merge: don't compact the corpus before fuzzing
        bool mergeOnly = false;         // --merge-only: compact the corpus and exit (no fuzzing)

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seconds": seconds = int.Parse(args[++i]); break;
                case "--passes": passes = int.Parse(args[++i]); break;
                case "--workers": workers = Math.Max(1, int.Parse(args[++i])); break;
                case "--only": only = args[++i]; break;
                case "--bin": binOverride = args[++i]; break;
                case "--libfuzzer": libfuzzer = args[++i]; break;
                case "--skip-instrument": skipInstrument = true; break;
                case "--no-merge": skipMerge = true; break;
                case "--merge-only": mergeOnly = true; break;
                default:
                    Console.Error.WriteLine($"unknown option: {args[i]}");
                    return 2;
            }
        }

        string bin = binOverride ?? AppContext.BaseDirectory;
        string exe = Path.Combine(bin, "asm-fuzz.exe");
        if (!File.Exists(exe))
        {
            Console.Error.WriteLine($"asm-fuzz.exe not found at '{exe}'. Pass --bin <dir> to point at the build output.");
            return 2;
        }

        string projectDir = LocateProjectDir();
        string dict = Path.Combine(projectDir, "asm.dict");

        string[] targets = only != null
            ? [.. only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : FuzzTargetNames.All;

        Console.WriteLine("fuzz-all (coverage-guided round-robin)");
        Console.WriteLine($"  bin:     {bin}");
        Console.WriteLine($"  targets: {targets.Length} ({seconds}s each, {(passes == 0 ? "looping" : passes + " pass(es)")})");
        Console.WriteLine($"  workers: {workers}{(workers > 1 ? $" (parallel; {Environment.ProcessorCount} cores available)" : " (single; pass --workers N to parallelize)")}");
        Console.WriteLine();

        // 1. Instrument the DLLs once. (sharpfuzz rewrites IL to emit edge coverage.)
        if (!skipInstrument)
        {
            foreach (string lib in FuzzTargetNames.LibrariesToInstrument)
            {
                string dll = Path.Combine(bin, lib);
                if (!File.Exists(dll))
                {
                    Console.Error.WriteLine($"  skip instrument (missing): {dll}");
                    continue;
                }

                Console.WriteLine($"=== instrumenting {lib} ===");
                if (!Instrument(dll))
                {
                    return 1;
                }
            }

            Console.WriteLine();
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // stop after the current slice instead of killing the orchestrator
            _stop = true;
            Console.WriteLine();
            Console.WriteLine("(stopping after current target — Ctrl+C)");
        };

        // 2. Compact each target's persistent corpus to a much smaller covering set with the SAME coverage
        // (libFuzzer -merge — greedy/order-dependent, so coverage-preserving but NOT provably minimal).
        // Coverage-guided fuzzing keeps every coverage-increasing input forever, so without this the on-disk
        // corpus grows unbounded across runs (it had reached ~470k files / 850 MB). Compacting here — before
        // fuzzing — bounds it: each run starts from the reduced set, grows during the run, and is
        // re-compacted at the next run's start. --no-merge skips it; --merge-only does just this and exits.
        if (!skipMerge || mergeOnly)
        {
            Console.WriteLine("=== compacting corpora (libFuzzer -merge; coverage-preserving, greedy not minimal) ===");
            foreach (string target in targets)
            {
                if (_stop)
                {
                    break;
                }

                string corpus = Path.Combine(projectDir, "corpus", target);
                Directory.CreateDirectory(corpus);
                if (!MinimizeCorpus(libfuzzer, exe, target, corpus, projectDir))
                {
                    // Tool missing / merge failed: warn and carry on — minimization is an optimization,
                    // not a prerequisite for fuzzing. (MinimizeCorpus already printed the reason.)
                    if (mergeOnly)
                    {
                        return 1;
                    }
                }
            }

            Console.WriteLine();
        }

        if (mergeOnly)
        {
            Console.WriteLine("fuzz-all: merge-only done.");
            return 0;
        }

        // 3. Round-robin the coverage-guided driver over the targets, with persistent corpora.
        int pass = 0;
        while (!_stop && (passes == 0 || pass < passes))
        {
            pass++;
            Console.WriteLine($"########## pass {pass} ##########");

            foreach (string target in targets)
            {
                if (_stop)
                {
                    break;
                }

                string corpus = Path.Combine(projectDir, "corpus", target);
                Directory.CreateDirectory(corpus);

                Console.WriteLine();
                Console.WriteLine($"########## {target}  (pass {pass}, {seconds}s, {workers} worker(s)) ##########");
                if (!RunLibFuzzer(libfuzzer, exe, target, dict, corpus, seconds, workers, projectDir))
                {
                    return 1; // tool missing — message already printed
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("fuzz-all: done.");
        return 0;
    }

    private static bool Instrument(string dllPath)
    {
        try
        {
            var psi = new ProcessStartInfo("sharpfuzz", $"\"{dllPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (output.Length > 0)
            {
                Console.Write(output.EndsWith('\n') ? output : output + Environment.NewLine);
            }

            if (p.ExitCode == 0)
            {
                return true;
            }

            // sharpfuzz exits non-zero with "The specified assembly is already instrumented." when
            // fuzz-all is re-run without an intervening rebuild. That's fine — the DLL is ready to
            // fuzz, so treat it as success rather than aborting the whole campaign.
            if (output.Contains("already instrumented", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  (already instrumented — reusing; rebuild to re-instrument)");
                return true;
            }

            Console.Error.WriteLine($"sharpfuzz failed for {dllPath} (exit {p.ExitCode}).");
            return false;
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine("'sharpfuzz' not found. Install it once with:");
            Console.Error.WriteLine("    dotnet tool install --global SharpFuzz.CommandLine");
            return false;
        }
    }

    /// <summary>
    /// Compacts <paramref name="corpus"/> in place to a much smaller set that preserves the same edge
    /// coverage, via <c>libfuzzer-dotnet -merge=1</c>. Merges the source corpus into a fresh empty directory
    /// (so the result is a covering subset), then atomically swaps it in. NOTE: <c>-merge</c> is a GREEDY,
    /// order-dependent reduction — it keeps an input whenever it adds any new edge — so the result is
    /// coverage-preserving and far smaller, but NOT a provably minimal set, and not even a fixed point
    /// (re-running trims a little more each pass before converging). Returns false only when the merge tool
    /// is missing or the process failed — compaction is best-effort, so callers treat false as "skip and
    /// keep fuzzing", not a fatal error.
    /// </summary>
    private static bool MinimizeCorpus(string libfuzzer, string exe, string target, string corpus, string projectDir)
    {
        int before = Directory.EnumerateFiles(corpus).Count();
        if (before <= 1)
        {
            return true; // nothing to compact
        }

        string minimized = Path.Combine(projectDir, "corpus", $".merge-{target}");
        if (Directory.Exists(minimized))
        {
            Directory.Delete(minimized, recursive: true);
        }

        Directory.CreateDirectory(minimized);

        var psi = new ProcessStartInfo(libfuzzer)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add($"--target_path={exe}");
        psi.ArgumentList.Add($"--target_arg={target}");
        psi.ArgumentList.Add("-merge=1");
        psi.ArgumentList.Add($"-max_len={FuzzLimits.MaxInputLength}");
        psi.ArgumentList.Add(minimized); // DST — the reduced covering set is written here
        psi.ArgumentList.Add(corpus);    // SRC — the (possibly bloated) persistent corpus

        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");
            // -merge prints a long per-input trace; we only care about the outcome, so drain quietly.
            _ = p.StandardOutput.ReadToEnd();
            _ = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                Console.Error.WriteLine($"  {target}: merge exited {p.ExitCode}; leaving corpus untouched.");
                Directory.Delete(minimized, recursive: true);
                return false;
            }

            // Swap: replace the bloated corpus with the minimized set.
            Directory.Delete(corpus, recursive: true);
            Directory.Move(minimized, corpus);

            int after = Directory.EnumerateFiles(corpus).Count();
            Console.WriteLine($"  {target}: {before} -> {after} inputs");
            return true;
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine($"  {target}: '{libfuzzer}' not found — skipping corpus minimization.");
            if (Directory.Exists(minimized))
            {
                Directory.Delete(minimized, recursive: true);
            }

            return false;
        }
    }

    private static bool RunLibFuzzer(string libfuzzer, string exe, string target, string dict, string corpus, int seconds, int workers, string projectDir)
    {
        var psi = new ProcessStartInfo(libfuzzer) { UseShellExecute = false };
        psi.ArgumentList.Add($"--target_path={exe}");
        psi.ArgumentList.Add($"--target_arg={target}");
        if (File.Exists(dict))
        {
            psi.ArgumentList.Add($"-dict={dict}");
        }

        psi.ArgumentList.Add($"-max_len={FuzzLimits.MaxInputLength}");
        psi.ArgumentList.Add($"-max_total_time={seconds}");

        string? worker0Log = null;
        if (workers > 1)
        {
            // Parallel mode: libFuzzer runs `workers` jobs concurrently (separate processes) sharing
            // and periodically merging the one corpus dir. Their per-exec output goes to fuzz-<n>.log
            // (the master is otherwise quiet for the whole slice), and crash artifacts land under the
            // log dir. We tail fuzz-0.log to the console below so progress stays visible.
            string logDir = Path.Combine(projectDir, "fuzz-logs", target);
            Directory.CreateDirectory(logDir);
            psi.ArgumentList.Add($"-workers={workers}");
            psi.ArgumentList.Add($"-jobs={workers}");
            psi.ArgumentList.Add($"-artifact_prefix={logDir}{Path.DirectorySeparatorChar}");
            psi.WorkingDirectory = logDir;
            worker0Log = Path.Combine(logDir, "fuzz-0.log");
            Console.WriteLine($"  parallel: {workers} workers — logs + crashes in {logDir} (tailing worker 0 below)");
        }

        psi.ArgumentList.Add(corpus);

        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Process.Start returned null");

            if (worker0Log is not null)
            {
                // libFuzzer's master is silent in -jobs mode; stream worker 0's log so the run is visible.
                var tail = new Thread(() => TailToConsole(worker0Log, () => !p.HasExited && !_stop)) { IsBackground = true };
                tail.Start();
                p.WaitForExit();
                tail.Join(2000);
            }
            else
            {
                p.WaitForExit();
            }

            // libFuzzer exits non-zero when it finds a crash; that is a result, not a driver failure.
            return true;
        }
        catch (Win32Exception)
        {
            Console.Error.WriteLine($"'{libfuzzer}' not found. Download libfuzzer-dotnet.exe from");
            Console.Error.WriteLine("    https://github.com/Metalnem/libfuzzer-dotnet/releases");
            Console.Error.WriteLine("and put it on PATH, or pass --libfuzzer <path>.");
            return false;
        }
    }

    /// <summary>
    /// Tails a growing log file to the console (prefixed <c>[w0]</c>) until <paramref name="keepGoing"/>
    /// returns false, then does one final read to flush the tail. Tolerant of the file not existing yet
    /// and of concurrent writes (opened with <see cref="FileShare.ReadWrite"/>).
    /// </summary>
    private static void TailToConsole(string path, Func<bool> keepGoing)
    {
        long pos = 0;
        while (true)
        {
            bool go = keepGoing();
            try
            {
                if (File.Exists(path))
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (fs.Length > pos)
                    {
                        fs.Seek(pos, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs);
                        string? line;
                        while ((line = sr.ReadLine()) is not null)
                        {
                            Console.WriteLine($"[w0] {line}");
                        }

                        pos = fs.Position;
                    }
                }
            }
            catch
            {
                // transient IO/sharing while the worker writes — retry next tick
            }

            if (!go)
            {
                break;
            }

            Thread.Sleep(400);
        }
    }

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
}
