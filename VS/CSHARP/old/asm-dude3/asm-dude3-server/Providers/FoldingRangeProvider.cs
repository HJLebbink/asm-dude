using Microsoft.Extensions.Logging;

namespace AsmDude3.Server.Providers;

public record FoldingRangeInfo
{
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public int? StartCharacter { get; init; }
    public int? EndCharacter { get; init; }
    public string? Kind { get; init; }
    public string? CollapsedText { get; init; }
}

public class FoldingRangeProvider
{
    private readonly ILogger _logger;
    private readonly string _startKeyword;
    private readonly string _endKeyword;

    public FoldingRangeProvider(ILogger logger, string startKeyword = "#region", string endKeyword = "#endregion")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _startKeyword = startKeyword;
        _endKeyword = endKeyword;
    }

    public List<FoldingRangeInfo> ProvideFoldingRanges(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var startKeywordUpper = _startKeyword.ToUpperInvariant();
        var endKeywordUpper = _endKeyword.ToUpperInvariant();
        int startKeywordLength = startKeywordUpper.Length;
        int endKeywordLength = endKeywordUpper.Length;

        var foldingRanges = new List<FoldingRangeInfo>();
        var startLineNumbers = new Stack<int>();
        var startCharacters = new Stack<int>();

        for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            string lineStr = lines[lineNumber].ToUpperInvariant();
            int offsetRegion = lineStr.IndexOf(startKeywordUpper, StringComparison.Ordinal);

            if (offsetRegion != -1)
            {
                startLineNumbers.Push(lineNumber);
                startCharacters.Push(offsetRegion);
            }
            else
            {
                int offsetEndRegion = lineStr.IndexOf(endKeywordUpper, StringComparison.Ordinal);
                if (offsetEndRegion != -1)
                {
                    if (startLineNumbers.Count == 0)
                    {
                        _logger.LogWarning("Line {Line}: keyword {EndKeyword} has no matching {StartKeyword}",
                            lineNumber, _endKeyword, _startKeyword);
                    }
                    else
                    {
                        int startLine = startLineNumbers.Pop();
                        int startCharacter = startCharacters.Pop();

                        foldingRanges.Add(new FoldingRangeInfo
                        {
                            StartLine = startLine,
                            StartCharacter = startCharacter,
                            EndLine = lineNumber,
                            EndCharacter = offsetEndRegion + endKeywordLength,
                            Kind = "region",
                            CollapsedText = GetCollapsedText(startCharacter + startKeywordLength + 1, lines[startLine])
                        });
                    }
                }
            }
        }

        return foldingRanges;
    }

    private static string GetCollapsedText(int startPos, string line)
    {
        int length = line.Length - startPos;
        if (length <= 0)
        {
            return "...";
        }
        return line.Substring(startPos, length).Trim();
    }
}
