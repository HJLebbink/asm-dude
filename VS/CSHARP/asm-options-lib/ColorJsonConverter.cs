// Copyright (c) 2026 Henk-Jan Lebbink
// Licensed under the MIT license.

using System;
using System.Drawing;
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
                int argb = Convert.ToInt32(value[1..], 16);
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
                        r = reader.GetInt32();
                        break;
                    case "G":
                        g = reader.GetInt32();
                        break;
                    case "B":
                        b = reader.GetInt32();
                        break;
                    case "A":
                        a = reader.GetInt32();
                        break;
                    case "NAME":
                        name = reader.GetString() ?? name;
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
            return Color.FromArgb(a, r, g, b);
        }

        // Skip unknown token types gracefully
        reader.Skip();
        return Color.Empty;
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
