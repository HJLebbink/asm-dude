using Microsoft.Extensions.Logging;
using AsmTools;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Provides document highlights (occurrences of a symbol)
/// </summary>
public class DocumentHighlightProvider
{
    private readonly ILogger _logger;

    public DocumentHighlightProvider(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Find word boundary at a given position
    /// </summary>
    private static (int startPos, int endPos) FindWordBoundary(int position, string lineStr)
    {
        int lineLength = lineStr.Length;
        if (position >= lineLength)
        {
            return (-1, -1);
        }
        if (AsmTools.AsmSourceTools.IsSeparatorChar(lineStr[position]))
        {
            return (-1, -1);
        }

        int startPos = 0;
        int endPos = lineLength;
        char[] lineChars = lineStr.ToCharArray(0, lineLength);

        for (int i = position + 1; i < lineLength; ++i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
            {
                endPos = i;
                break;
            }
        }
        for (int i = position; i >= 0; --i)
        {
            if (AsmTools.AsmSourceTools.IsSeparatorChar(lineChars[i]))
            {
                startPos = i + 1;
                break;
            }
        }
        return (startPos, endPos);
    }

    /// <summary>
    /// Get character at offset, returning space if out of bounds
    /// </summary>
    private static char GetChar(string str, int offset)
    {
        return ((offset < 0) || (offset >= str.Length)) ? ' ' : str[offset];
    }

    /// <summary>
    /// Get highlight range for a specific word
    /// </summary>
    private static HighlightRange? GetHighlightRange(string lineStr, int lineOffset, ref int characterOffset, string wordToMatch)
    {
        int wordLength = wordToMatch.Length;

        if ((characterOffset + wordLength) <= lineStr.Length)
        {
            char before = GetChar(lineStr, characterOffset - 1);
            char after = GetChar(lineStr, characterOffset + wordLength);

            if (!AsmTools.AsmSourceTools.IsSeparatorChar(before) || !AsmTools.AsmSourceTools.IsSeparatorChar(after))
            {
                return null;
            }

            string subString = lineStr.Substring(characterOffset, wordLength);
            if (subString.Equals(wordToMatch, StringComparison.OrdinalIgnoreCase))
            {
                return new HighlightRange
                {
                    StartLine = lineOffset,
                    StartChar = characterOffset,
                    EndLine = lineOffset,
                    EndChar = characterOffset + wordLength
                };
            }
        }
        return null;
    }

    /// <summary>
    /// Get highlight range for multiple possible words
    /// </summary>
    private static HighlightRange? GetHighlightRangeMultiple(string line, int lineOffset, ref int characterOffset, IEnumerable<string> wordsToMatch)
    {
        foreach (string wordToMatch in wordsToMatch)
        {
            var range = GetHighlightRange(line, lineOffset, ref characterOffset, wordToMatch);
            if (range != null)
            {
                return range;
            }
        }
        return null;
    }

    /// <summary>
    /// Provide document highlights for all occurrences of the symbol at the given position
    /// </summary>
    public List<DocumentHighlightInfo>? ProvideDocumentHighlights(string[] lines, int lineNumber, int character)
    {
        if (lineNumber < 0 || lineNumber >= lines.Length)
        {
            return null;
        }

        string lineStr = lines[lineNumber];
        (int startPos, int endPos) = FindWordBoundary(character, lineStr);
        int length = endPos - startPos;

        if (length <= 0)
        {
            _logger.LogDebug("DocumentHighlightProvider: word length too small ({Length})", length);
            return null;
        }

        string currentHighlightedWord = lineStr.Substring(startPos, length);
        if (string.IsNullOrEmpty(currentHighlightedWord))
        {
            _logger.LogDebug("DocumentHighlightProvider: word is empty");
            return null;
        }

        // Check if it's a register - if so, include related registers
        IList<string> currentHighlightedWords = new List<string>();
        Rn reg = RegisterTools.ParseRn(currentHighlightedWord, false);
        if (reg == Rn.NOREG)
        {
            currentHighlightedWords.Add(currentHighlightedWord);
        }
        else
        {
            // Add all related registers (e.g., RAX, EAX, AX, AH, AL)
            foreach (string x in RegisterTools.GetRelatedRegisterNew(reg))
            {
                currentHighlightedWords.Add(x);
            }
        }

        _logger.LogDebug("DocumentHighlightProvider: highlighting words: {Words}",
            string.Join(", ", currentHighlightedWords));

        List<DocumentHighlightInfo> highlights = new();

        // Search through all lines for occurrences
        for (int i = 0; i < lines.Length; i++)
        {
            string currentLine = lines[i];

            for (int j = 0; j < currentLine.Length; j++)
            {
                var range = GetHighlightRangeMultiple(currentLine, i, ref j, currentHighlightedWords);
                if (range != null)
                {
                    j++; // Move past the matched word
                    highlights.Add(new DocumentHighlightInfo
                    {
                        Range = range,
                        Kind = DocumentHighlightKind.Text
                    });
                }
            }
        }

        return highlights.Count > 0 ? highlights : null;
    }
}

/// <summary>
/// Document highlight information
/// </summary>
public record DocumentHighlightInfo
{
    public required HighlightRange Range { get; init; }
    public DocumentHighlightKind Kind { get; init; }
}

/// <summary>
/// Highlight range
/// </summary>
public record HighlightRange
{
    public required int StartLine { get; init; }
    public required int StartChar { get; init; }
    public required int EndLine { get; init; }
    public required int EndChar { get; init; }
}

/// <summary>
/// Document highlight kind
/// </summary>
public enum DocumentHighlightKind
{
    Text = 1,
    Read = 2,
    Write = 3
}
