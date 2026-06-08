namespace AsmFuzz;

/// <summary>
/// Exception policy for the LS/semantic fuzz targets.
/// <para>
/// SharpFuzz/libFuzzer detect defects by observing <b>unhandled</b> exceptions. A blanket
/// <c>catch { }</c> therefore eats the <see cref="NullReferenceException"/> /
/// <see cref="IndexOutOfRangeException"/> / <see cref="ArgumentOutOfRangeException"/> that fuzzing
/// is best at finding, leaving the LS targets able to catch only hangs/OOM/AVs.
/// </para>
/// <para>
/// The invariant we want to enforce is: <b>the language server must not throw on any document bytes
/// or in-range position</b>. So <see cref="Guard"/> swallows only the narrow set of genuinely
/// expected exceptions (see <see cref="IsExpected"/>) and rethrows everything else, letting libFuzzer
/// record the crash.
/// </para>
/// </summary>
internal static class FuzzGuard
{
    /// <summary>
    /// True only for exceptions that are a legitimate, non-bug outcome of fuzzing the LS.
    /// Currently just cancellation (the targets pass <see cref="CancellationToken.None"/>, but a
    /// cancelled operation is never a defect). Everything else is treated as a real bug.
    /// </summary>
    public static bool IsExpected(Exception ex) => ex is OperationCanceledException;

    /// <summary>
    /// Runs <paramref name="action"/>, swallowing only expected exceptions and rethrowing the rest
    /// so the fuzzer surfaces them.
    /// </summary>
    public static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            // Expected outcome — keep fuzzing.
        }
    }
}
