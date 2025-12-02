namespace AsmDude3.Server.Providers;

/// <summary>
/// Result of symbolic analysis on assembly code
/// </summary>
public record SymbolicAnalysisResult
{
    /// <summary>
    /// Register and flag states at each line number
    /// Key: Line number, Value: State at that line
    /// </summary>
    public required Dictionary<int, RegisterState> RegisterStates { get; init; }

    /// <summary>
    /// Diagnostics found during analysis (warnings, errors, info)
    /// </summary>
    public required List<DiagnosticInfo> Diagnostics { get; init; }

    /// <summary>
    /// Optimization hints for improving code
    /// </summary>
    public required List<OptimizationHint> Optimizations { get; init; }
}

/// <summary>
/// Register and flag values at a specific program point
/// </summary>
public record RegisterState
{
    /// <summary>
    /// Line number this state represents
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Inferred register values
    /// Key: Register name (e.g., "RAX"), Value: Hex string (e.g., "0x000000000000000A")
    /// </summary>
    public required Dictionary<string, string> RegisterValues { get; init; }

    /// <summary>
    /// Inferred flag values
    /// Key: Flag name (e.g., "ZF", "CF"), Value: true/false if known
    /// </summary>
    public required Dictionary<string, bool> FlagValues { get; init; }
}

/// <summary>
/// Diagnostic issue found during analysis
/// </summary>
public record DiagnosticInfo
{
    /// <summary>
    /// Line number where issue occurs
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Severity of the diagnostic
    /// </summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// Diagnostic message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Diagnostic code (optional)
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Character range within the line (optional)
    /// </summary>
    public DiagnosticRange? Range { get; init; }
}

/// <summary>
/// Character range for a diagnostic
/// </summary>
public record DiagnosticRange
{
    public required int StartChar { get; init; }
    public required int EndChar { get; init; }
}

/// <summary>
/// Severity levels for diagnostics
/// </summary>
public enum DiagnosticSeverity
{
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

/// <summary>
/// Optimization suggestion for code improvement
/// </summary>
public record OptimizationHint
{
    /// <summary>
    /// Line number for the optimization
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Short title for the optimization
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed description of the optimization
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Suggested replacement code (optional)
    /// </summary>
    public string? Replacement { get; init; }
}
