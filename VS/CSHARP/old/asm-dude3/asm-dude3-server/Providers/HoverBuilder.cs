using AsmDude3.Server.Protocol;
using System.Collections.Generic;
using System.Linq;

namespace AsmDude3.Server.Providers;

/// <summary>
/// Builds hover responses with clickable hyperlinks using VSInternalHover
/// </summary>
public static class HoverBuilder
{
    /// <summary>
    /// Create a hover with clickable hyperlink using VSInternalHover
    /// This uses custom types that serialize to VS-compatible JSON
    /// </summary>
    public static VSInternalHover CreateHoverWithLink(string text, string? url, string description, int line, int startChar, int endChar)
    {
        var runs = new List<ClassifiedTextRun>();

        // Create hyperlink run if URL exists
        if (!string.IsNullOrEmpty(url))
        {
            runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, text, url));
        }
        else
        {
            runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Keyword, text));
        }

        // Add description
        if (!string.IsNullOrEmpty(description))
        {
            runs.Add(new ClassifiedTextRun(PredefinedClassificationTypeNames.Text, ": " + description));
        }

        // Create classified text element
        var textElement = new ClassifiedTextElement(runs.ToArray());

        // Create container
        var container = new ContainerElement(ContainerElementStyle.Wrapped, textElement);

        // Create VSInternalHover with both standard Contents and RawContent
        var hover = new VSInternalHover
        {
            Contents = new AsmDude3.Server.MarkupContent
            {
                Kind = "markdown",
                Value = !string.IsNullOrEmpty(url) ? $"[{text}]({url}): {description}" : $"{text}: {description}"
            },
            Range = new AsmDude3.Server.Range
            {
                Start = new AsmDude3.Server.Position { Line = line, Character = startChar },
                End = new AsmDude3.Server.Position { Line = line, Character = endChar }
            },
            RawContent = container
        };

        return hover;
    }

    /// <summary>
    /// Create a standard hover without hyperlink
    /// </summary>
    public static AsmDude3.Server.Hover CreateStandardHover(string content, int line, int startChar, int endChar)
    {
        return new AsmDude3.Server.Hover
        {
            Contents = new AsmDude3.Server.MarkupContent
            {
                Kind = "markdown",
                Value = content
            },
            Range = new AsmDude3.Server.Range
            {
                Start = new AsmDude3.Server.Position { Line = line, Character = startChar },
                End = new AsmDude3.Server.Position { Line = line, Character = endChar }
            }
        };
    }
}
