// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

namespace AsmDude2.Vsix.Tests;

using AsmDude2;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

/// <summary>
/// Tests for <see cref="LogFileTail.PumpAsync"/> — the deterministic core of the "AsmSim" output pane that
/// tails the out-of-process sim server's log file into a VS pane. Exercises the real production logic (the
/// file is linked into this project), so they catch a regression in the append/position/truncation handling
/// without needing the VS.Extensibility SDK or a live editor.
/// </summary>
public class LogFileTailTests
{
    private static (Func<string, Task> emit, List<string> captured) Sink()
    {
        var captured = new List<string>();
        Func<string, Task> emit = line => { captured.Add(line); return Task.CompletedTask; };
        return (emit, captured);
    }

    [Fact]
    public async Task PumpAsync_EmitsAppendedLines_AndAdvancesPosition()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "alpha" + Environment.NewLine + "beta" + Environment.NewLine, TestContext.Current.CancellationToken);
            (Func<string, Task> emit, List<string> captured) = Sink();

            long pos = await LogFileTail.PumpAsync(path, 0, emit, CancellationToken.None);

            Assert.Equal(new[] { "alpha", "beta" }, captured);

            // Append more; a second pump from the returned position must emit ONLY the new line.
            await File.AppendAllTextAsync(path, "gamma" + Environment.NewLine, TestContext.Current.CancellationToken);
            captured.Clear();
            await LogFileTail.PumpAsync(path, pos, emit, CancellationToken.None);

            Assert.Equal(new[] { "gamma" }, captured);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PumpAsync_FileTruncated_RestartsFromTop()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "a long previous line" + Environment.NewLine, TestContext.Current.CancellationToken);
            (Func<string, Task> emit, List<string> captured) = Sink();
            long pos = await LogFileTail.PumpAsync(path, 0, emit, CancellationToken.None);

            // Rotate: overwrite with shorter content (new length < previous position).
            await File.WriteAllTextAsync(path, "fresh" + Environment.NewLine, TestContext.Current.CancellationToken);
            captured.Clear();
            await LogFileTail.PumpAsync(path, pos, emit, CancellationToken.None);

            Assert.Equal(new[] { "fresh" }, captured); // restarted at 0 instead of missing the line
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PumpAsync_MissingFile_IsNoOp()
    {
        string path = Path.Combine(Path.GetTempPath(), "asmdude-no-such-file-" + Guid.NewGuid().ToString("N") + ".log");
        (Func<string, Task> emit, List<string> captured) = Sink();

        long pos = await LogFileTail.PumpAsync(path, 123, emit, CancellationToken.None);

        Assert.Empty(captured);
        Assert.Equal(123, pos); // unchanged
    }

    [Fact]
    public async Task PumpAsync_NoNewContent_EmitsNothing()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "only line" + Environment.NewLine, TestContext.Current.CancellationToken);
            (Func<string, Task> emit, List<string> captured) = Sink();
            long pos = await LogFileTail.PumpAsync(path, 0, emit, CancellationToken.None);
            captured.Clear();

            // Pump again with no new appends.
            long pos2 = await LogFileTail.PumpAsync(path, pos, emit, CancellationToken.None);

            Assert.Empty(captured);
            Assert.Equal(pos, pos2);
        }
        finally { File.Delete(path); }
    }
}
