using Newtonsoft.Json;

namespace AsmDude3.Server.Protocol;

/// <summary>
/// Custom types that serialize to VS-compatible JSON for clickable hyperlinks
/// These mimic the internal VS types but are defined locally to avoid dependency on unavailable packages
/// </summary>

public class VSInternalHover
{
    [JsonProperty("contents")]
    public object? Contents { get; set; }

    [JsonProperty("range")]
    public AsmDude3.Server.Range? Range { get; set; }

    [JsonProperty("rawContent")]
    public object? RawContent { get; set; }
}

public class ClassifiedTextElement
{
    [JsonProperty("runs")]
    public ClassifiedTextRun[] Runs { get; set; } = Array.Empty<ClassifiedTextRun>();

    public ClassifiedTextElement(params ClassifiedTextRun[] runs)
    {
        Runs = runs;
    }
}

public class ClassifiedTextRun
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("classificationType")]
    public string? ClassificationType { get; set; }

    [JsonProperty("markerTagType")]
    public string? MarkerTagType { get; set; }

    [JsonProperty("navigationAction")]
    public string? NavigationAction { get; set; }

    public ClassifiedTextRun(string classificationType, string text, string? navigationAction = null)
    {
        ClassificationType = classificationType;
        Text = text;
        NavigationAction = navigationAction;
    }
}

public class ContainerElement
{
    [JsonProperty("style")]
    public int Style { get; set; }

    [JsonProperty("elements")]
    public object[] Elements { get; set; } = Array.Empty<object>();

    public ContainerElement(int style, params object[] elements)
    {
        Style = style;
        Elements = elements;
    }
}

public static class ContainerElementStyle
{
    public const int Wrapped = 0;
    public const int Stacked = 1;
}

public static class PredefinedClassificationTypeNames
{
    public const string Keyword = "keyword";
    public const string Text = "text";
    public const string WhiteSpace = "whitespace";
    public const string Operator = "operator";
    public const string Identifier = "identifier";
    public const string Literal = "literal";
    public const string Number = "number";
    public const string String = "string";
    public const string Comment = "comment";
}
