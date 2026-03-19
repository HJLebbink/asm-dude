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

// ============================================================================
// Custom types that serialize to VS-compatible JSON for styled hover tooltips.
//
// These mimic the internal VS types (Microsoft.VisualStudio.Text.Adornments)
// but are defined locally because they are not available in any public NuGet
// package. VS deserializes these via its internal ObjectContentConverter,
// matching on the "_vs_type" discriminator and PascalCase property names.
//
// IMPORTANT — CLICKABLE LINKS ARE NOT POSSIBLE OVER LSP:
//
//   The WPF ClassifiedTextRun.NavigationAction is an Action *delegate*
//   (a C# callback), not a URL string. Delegates cannot be serialized
//   over JSON-RPC. Even Roslyn explicitly sets navigationActionFactory: null
//   when building LSP hover responses with the comment:
//     "Build the classified text without navigation actions - they are not serializable."
//   See: dotnet/roslyn src/LanguageServer/Protocol/Handler/Hover/HoverHandler.cs
//
//   To get clickable links in hover, you need in-process MEF access to
//   IAsyncQuickInfoSource, where you can create WPF ClassifiedTextRun objects
//   with real Action delegates (e.g. () => Process.Start(url)).
//   This requires a hybrid VSSDK+VisualStudio.Extensibility extension with
//   RequiresInProcessHosting = true. The old archived code in
//   VS/CSHARP/old/asm-dude2-vsix-archived/QuickInfo/AsmQuickInfoSource.cs
//   shows how this was done with the in-process VSSDK extension.
//
//   The current OOP extension architecture does not support this.
//   See: https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/get-started/in-proc-extensions
//
// WHAT WORKS over LSP:
//   - Classified (colored) text via ClassifiedTextRun with ClassificationTypeName
//   - Monospace font via ClassifiedTextRunStyle.UseClassificationFont with "formal language"
//   - Bold, italic, underline via ClassifiedTextRunStyle flags
//   - Stacked/wrapped layout via ContainerElement
//   - VS renders _vs_rawContent instead of standard Contents when present
// ============================================================================

/// <summary>
/// VS-internal hover type with RawContent for styled text.
/// When RawContent is set, Contents should be null — VS renders both if both are present.
/// </summary>
public sealed class VSInternalHover
{
    [JsonPropertyName("contents")]
    public object? Contents { get; init; }

    [JsonPropertyName("range")]
    public Range? Range { get; init; }

    /// <summary>
    /// VS-specific property containing ClassifiedTextElement/ContainerElement for styled text.
    /// Uses _vs_rawContent to match Roslyn's serialization format.
    /// </summary>
    [JsonPropertyName("_vs_rawContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? RawContent { get; init; }
}

/// <summary>
/// Contains an array of ClassifiedTextRun for rendering styled text.
/// </summary>
public sealed class ClassifiedTextElement(params ClassifiedTextRun[] Runs)
{
    /// <summary>
    /// Type discriminator for VS serialization. Must match Roslyn's ObjectContentConverter.TypeProperty.
    /// </summary>
    [JsonPropertyName("_vs_type")]
    public string TypeName { get; } = "ClassifiedTextElement";

    [JsonPropertyName("Runs")]
    public ClassifiedTextRun[] Runs { get; } = Runs;
}

/// <summary>
/// A single run of styled text. Navigation actions are NOT supported over LSP (see file header).
/// Use ClassificationTypeName "formal language" with UseClassificationFont for monospace rendering.
/// </summary>
public sealed class ClassifiedTextRun(
    string ClassificationType,
    string Text,
    ClassifiedTextRunStyle Style = ClassifiedTextRunStyle.Plain,
    string? Tooltip = null)
{
    [JsonPropertyName("Text")]
    public string Text { get; } = Text;

    /// <summary>
    /// Must be "ClassificationTypeName" to match Roslyn's ClassifiedTextRunConverter.
    /// Use "formal language" for monospace font, "keyword" for colored keyword, etc.
    /// </summary>
    [JsonPropertyName("ClassificationTypeName")]
    public string ClassificationType { get; } = ClassificationType;

    [JsonPropertyName("MarkerTagType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MarkerTagType { get; init; }

    /// <summary>
    /// Text style flags. Use UseClassificationFont (0x8) with "formal language"
    /// classification to get monospace font rendering in hover tooltips.
    /// </summary>
    [JsonPropertyName("Style")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ClassifiedTextRunStyle Style { get; } = Style;

    /// <summary>
    /// Tooltip text shown when hovering over this run.
    /// </summary>
    [JsonPropertyName("Tooltip")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tooltip { get; } = Tooltip;
}

/// <summary>
/// Container for multiple elements with a specific layout style.
/// </summary>
public sealed class ContainerElement(ContainerElementStyle Style, params object[] Elements)
{
    /// <summary>
    /// Type discriminator for VS serialization.
    /// </summary>
    [JsonPropertyName("_vs_type")]
    public string TypeName { get; } = "ContainerElement";

    [JsonPropertyName("Style")]
    public ContainerElementStyle Style { get; } = Style;

    [JsonPropertyName("Elements")]
    public object[] Elements { get; } = Elements;
}

/// <summary>
/// Container element layout styles (flags).
/// </summary>
[Flags]
public enum ContainerElementStyle
{
    Wrapped = 0x0,
    Stacked = 0x1,
    VerticalPadding = 0x2,
}

/// <summary>
/// Text style flags for ClassifiedTextRun.
/// </summary>
[Flags]
public enum ClassifiedTextRunStyle
{
    Plain = 0x0,
    Bold = 0x1,
    Italic = 0x2,
    Underline = 0x4,

    /// <summary>
    /// Use the font associated with the classification type.
    /// Combine with "formal language" classification for monospace font.
    /// </summary>
    UseClassificationFont = 0x8,

    /// <summary>
    /// Use the style (bold/italic) associated with the classification type.
    /// </summary>
    UseClassificationStyle = 0x10,
}

/// <summary>
/// Predefined VS classification type names for text styling.
/// "formal language" is the key one for monospace font in hover tooltips.
/// </summary>
public static class PredefinedClassificationTypeNames
{
    /// <summary>
    /// Renders in monospace font when combined with UseClassificationFont style.
    /// This is what the old in-process VSIX used for hover text.
    /// </summary>
    public const string FormalLanguage = "formal language";

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
