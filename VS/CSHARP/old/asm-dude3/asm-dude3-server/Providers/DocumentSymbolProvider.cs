using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides document symbols (labels, functions, sections) for outline view
/// </summary>
public class DocumentSymbolProvider
{
    private readonly ILogger _logger;

    // Pattern for labels: identifier followed by colon
    private static readonly Regex LabelPattern = new(@"^\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*:", RegexOptions.Compiled);

    // Pattern for section directives
    private static readonly Regex SectionPattern = new(@"^\s*(\.[a-zA-Z_][a-zA-Z0-9_.]*)", RegexOptions.Compiled);

    // Common function entry point names
    private static readonly HashSet<string> FunctionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main", "_start", "_main", "WinMain", "DllMain", "start", "entry", "_entry",
        "wmain", "_wmain", "wWinMain"
    };

    // Common data label patterns
    private static readonly string[] DataKeywords = new[]
    {
        "data", "buffer", "string", "table", "array", "value", "const",
        "var", "msg", "message", "text", "ptr", "addr"
    };

    public DocumentSymbolProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Provide document symbols for the given lines
    /// </summary>
    public List<DocumentSymbolInfo> ProvideSymbols(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var symbols = new List<DocumentSymbolInfo>();

        for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var line = lines[lineNumber];

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Skip comments
            var commentIndex = line.IndexOf(';');
            var effectiveLine = commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
            if (string.IsNullOrWhiteSpace(effectiveLine))
            {
                continue;
            }

            // Check for section directives (.text, .data, .bss, etc.)
            var sectionMatch = SectionPattern.Match(effectiveLine);
            if (sectionMatch.Success)
            {
                var sectionName = sectionMatch.Groups[1].Value;
                var startChar = effectiveLine.IndexOf(sectionName);

                symbols.Add(new DocumentSymbolInfo
                {
                    Name = sectionName,
                    Kind = SymbolKind.Namespace,
                    Line = lineNumber,
                    Range = new SymbolRange
                    {
                        StartChar = startChar,
                        EndChar = startChar + sectionName.Length
                    }
                });
                continue;
            }

            // Check for labels
            var labelMatch = LabelPattern.Match(effectiveLine);
            if (labelMatch.Success)
            {
                var labelName = labelMatch.Groups[1].Value;
                var startChar = effectiveLine.IndexOf(labelName);
                var kind = ClassifyLabel(labelName);

                symbols.Add(new DocumentSymbolInfo
                {
                    Name = labelName,
                    Kind = kind,
                    Line = lineNumber,
                    Range = new SymbolRange
                    {
                        StartChar = startChar,
                        EndChar = startChar + labelName.Length
                    }
                });
            }
        }

        // Sort symbols by line number
        return symbols.OrderBy(s => s.Line).ToList();
    }

    /// <summary>
    /// Classify a label as function or data based on naming patterns
    /// </summary>
    private SymbolKind ClassifyLabel(string labelName)
    {
        // Check if it's a known function name
        if (FunctionNames.Contains(labelName))
        {
            return SymbolKind.Function;
        }

        // Check if label name contains data-related keywords
        var lowerName = labelName.ToLowerInvariant();
        if (DataKeywords.Any(keyword => lowerName.Contains(keyword)))
        {
            return SymbolKind.Variable;
        }

        // Local labels (starting with dot) are typically not functions
        if (labelName.StartsWith('.') && !labelName.Equals(".text") && !labelName.Equals(".data") && !labelName.Equals(".bss"))
        {
            return SymbolKind.Variable;
        }

        // Default to function for most labels
        return SymbolKind.Function;
    }
}
