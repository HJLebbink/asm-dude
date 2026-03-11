// The MIT License (MIT)
//
// Copyright (c) 2026 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Text.Json.Serialization;

using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace AsmDude2LS;

#nullable enable

/// <summary>
/// Custom types that serialize to VS-compatible JSON for clickable hyperlinks.
/// These mimic the internal VS types but are defined locally to avoid dependency on unavailable packages.
/// </summary>

/// <summary>
/// VS-internal hover type that supports clickable hyperlinks via RawContent.
/// </summary>
public sealed class VSInternalHover
{
    [JsonPropertyName("contents")]
    public object? Contents { get; init; }

    [JsonPropertyName("range")]
    public Range? Range { get; init; }

    /// <summary>
    /// VS-specific property that contains ClassifiedTextElement for clickable links.
    /// Uses _vs_rawContent to match Roslyn's serialization format.
    /// </summary>
    [JsonPropertyName("_vs_rawContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? RawContent { get; init; }
}

/// <summary>
/// Contains an array of ClassifiedTextRun for rendering styled/clickable text.
/// </summary>
/// <param name="Runs">The text runs to display.</param>
public sealed class ClassifiedTextElement(params ClassifiedTextRun[] Runs)
{
    /// <summary>
    /// Type discriminator for VS serialization.
    /// </summary>
    [JsonPropertyName("_vs_type")]
    public string TypeName { get; } = "ClassifiedTextElement";

    [JsonPropertyName("runs")]
    public ClassifiedTextRun[] Runs { get; } = Runs;
}

/// <summary>
/// A single run of styled text, optionally with a navigation action (URL).
/// Matches Roslyn's ClassifiedTextRun from Roslyn.Text.Adornments namespace.
/// </summary>
/// <param name="ClassificationType">The classification type for styling.</param>
/// <param name="Text">The text content.</param>
/// <param name="NavigationAction">Optional URL for navigation when clicked.</param>
/// <param name="Style">Optional text style (bold, italic, underline).</param>
/// <param name="Tooltip">Optional tooltip shown on hover.</param>
public sealed class ClassifiedTextRun(
    string ClassificationType,
    string Text,
    string? NavigationAction = null,
    ClassifiedTextRunStyle Style = ClassifiedTextRunStyle.Plain,
    string? Tooltip = null)
{
    [JsonPropertyName("text")]
    public string Text { get; } = Text;

    [JsonPropertyName("classificationType")]
    public string ClassificationType { get; } = ClassificationType;

    [JsonPropertyName("markerTagType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MarkerTagType { get; init; }

    /// <summary>
    /// Text style flags for bold, italic, underline formatting.
    /// </summary>
    [JsonPropertyName("style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ClassifiedTextRunStyle Style { get; } = Style;

    /// <summary>
    /// Tooltip text shown when hovering over this run.
    /// </summary>
    [JsonPropertyName("tooltip")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tooltip { get; } = Tooltip;

    /// <summary>
    /// URL for navigation when clicked - this is what makes hyperlinks work.
    /// </summary>
    [JsonPropertyName("navigationAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NavigationAction { get; } = NavigationAction;
}

/// <summary>
/// Container for multiple elements with a specific layout style.
/// </summary>
/// <param name="Style">The container layout style.</param>
/// <param name="Elements">The elements to contain.</param>
public sealed class ContainerElement(ContainerElementStyle Style, params object[] Elements)
{
    /// <summary>
    /// Type discriminator for VS serialization.
    /// </summary>
    [JsonPropertyName("_vs_type")]
    public string TypeName { get; } = "ContainerElement";

    [JsonPropertyName("style")]
    public ContainerElementStyle Style { get; } = Style;

    [JsonPropertyName("elements")]
    public object[] Elements { get; } = Elements;
}

/// <summary>
/// Container element layout styles (flags).
/// Matches Roslyn's ContainerElementStyle from Roslyn.Text.Adornments namespace.
/// </summary>
[Flags]
public enum ContainerElementStyle
{
    /// <summary>
    /// Elements are displayed end-to-end, wrapping as necessary.
    /// </summary>
    Wrapped = 0x0,

    /// <summary>
    /// Elements are displayed in a vertical stack.
    /// </summary>
    Stacked = 0x1,

    /// <summary>
    /// Additional vertical padding between elements.
    /// </summary>
    VerticalPadding = 0x2,
}

/// <summary>
/// Text style flags for ClassifiedTextRun.
/// Matches Roslyn's ClassifiedTextRunStyle from Roslyn.Text.Adornments namespace.
/// </summary>
[Flags]
public enum ClassifiedTextRunStyle
{
    /// <summary>
    /// Plain text with no special formatting.
    /// </summary>
    Plain = 0x0,

    /// <summary>
    /// Bold text.
    /// </summary>
    Bold = 0x1,

    /// <summary>
    /// Italic text.
    /// </summary>
    Italic = 0x2,

    /// <summary>
    /// Underlined text.
    /// </summary>
    Underline = 0x4,

    /// <summary>
    /// Use the font associated with the classification type.
    /// </summary>
    UseClassificationFont = 0x8,

    /// <summary>
    /// Use the style (bold/italic) associated with the classification type.
    /// </summary>
    UseClassificationStyle = 0x10,
}

/// <summary>
/// Predefined VS classification type names for text styling.
/// </summary>
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
