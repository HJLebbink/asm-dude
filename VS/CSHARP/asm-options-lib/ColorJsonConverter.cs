// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsmTools;

/// <summary>
/// System.Text.Json converter for System.Drawing.Color.
/// Lives in the shared contract lib so both the VSIX (settings producer) and the LSP server
/// (consumer) serialize/deserialize the Color fields of <see cref="AsmSettingsData"/> identically.
/// Also handles the JSON format produced by StreamJsonRpc when serializing Color values.
/// </summary>
public class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? value = reader.GetString();
            if (string.IsNullOrEmpty(value))
            {
                return Color.Empty;
            }
            // Handle named colors (e.g., "Blue") and hex colors (e.g., "#FF0000")
            if (value.StartsWith('#'))
            {
                // Tolerate malformed hex (non-hex chars, too many digits) by falling back to Empty
                // rather than letting Convert.ToInt32 throw — settings JSON is user-editable/fuzzable.
                if (!int.TryParse(value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
                {
                    return Color.Empty;
                }
                if (value.Length == 7) // #RRGGBB
                {
                    return Color.FromArgb(255, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
                }
                return Color.FromArgb(argb); // #AARRGGBB
            }
            var namedColor = Color.FromName(value);
            if (namedColor.IsKnownColor)
            {
                return namedColor;
            }
            return Color.Empty;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int r = 0, g = 0, b = 0, a = 255;
            string? name = null;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                string? propertyName = reader.GetString();
                reader.Read();

                switch (propertyName?.ToUpperInvariant())
                {
                    case "R":
                        r = ReadComponent(ref reader);
                        break;
                    case "G":
                        g = ReadComponent(ref reader);
                        break;
                    case "B":
                        b = ReadComponent(ref reader);
                        break;
                    case "A":
                        a = ReadComponent(ref reader);
                        break;
                    case "NAME":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            name = reader.GetString() ?? name;
                        }
                        else
                        {
                            reader.Skip();
                        }
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (name != null)
            {
                var namedColor = Color.FromName(name);
                if (namedColor.IsKnownColor)
                {
                    return namedColor;
                }
            }

            // Clamp to the valid [0,255] range so an out-of-range component (e.g. a typo or fuzzed
            // value) yields a clamped color instead of Color.FromArgb throwing ArgumentException —
            // which in production would otherwise reject the whole settings file.
            return Color.FromArgb(Math.Clamp(a, 0, 255), Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
        }

        // Skip unknown token types gracefully
        reader.Skip();
        return Color.Empty;
    }

    /// <summary>
    /// Reads an ARGB component value. Returns the integer when the current token is a number that fits
    /// in <see cref="int"/>; otherwise (a string/object/array/oversized number) ignores it — consuming
    /// any nested container — and returns 0, so a malformed component never throws.
    /// </summary>
    private static int ReadComponent(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int v))
        {
            return v;
        }

        reader.Skip();
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        if (value.IsNamedColor)
        {
            writer.WriteStringValue(value.Name);
        }
        else
        {
            writer.WriteStringValue($"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}");
        }
    }
}
