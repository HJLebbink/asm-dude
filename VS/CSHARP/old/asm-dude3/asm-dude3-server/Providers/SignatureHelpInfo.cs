namespace AsmDude3.Server.Providers;

/// <summary>
/// Represents signature help information (operand format hints)
/// </summary>
public record SignatureHelpInfo
{
    /// <summary>
    /// The available signatures
    /// </summary>
    public required List<SignatureInformation> Signatures { get; init; }

    /// <summary>
    /// The active signature index (which signature to highlight)
    /// </summary>
    public int ActiveSignature { get; init; }

    /// <summary>
    /// The active parameter index (which parameter to highlight)
    /// </summary>
    public int ActiveParameter { get; init; }
}

/// <summary>
/// Represents a single signature (operand format)
/// </summary>
public record SignatureInformation
{
    /// <summary>
    /// The label of this signature (e.g., "mov reg, reg")
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Documentation for this signature
    /// </summary>
    public string? Documentation { get; init; }

    /// <summary>
    /// Parameters in this signature
    /// </summary>
    public List<ParameterInformation>? Parameters { get; init; }
}

/// <summary>
/// Represents a parameter in a signature
/// </summary>
public record ParameterInformation
{
    /// <summary>
    /// The label of this parameter (e.g., "reg")
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Documentation for this parameter
    /// </summary>
    public string? Documentation { get; init; }
}
