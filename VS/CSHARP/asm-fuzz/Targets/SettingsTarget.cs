using AsmDude2LS;

using System.Text;
using System.Text.Json;

namespace AsmFuzz.Targets;

/// <summary>
/// Fuzz target for the settings-file deserialization path (didChangeConfiguration / settings.json →
/// <c>AsmLanguageServerOptions</c>), including the <c>ColorJsonConverter</c>. This exercises
/// <see cref="SettingsManager.DeserializeSettings"/> — the exact production code that turns the
/// VSIX-written settings file into server options — on arbitrary bytes.
/// <para>
/// Malformed JSON throwing <see cref="JsonException"/> is the EXPECTED outcome of random bytes and is
/// swallowed. Anything else (e.g. the color converter throwing on well-formed-but-odd JSON) propagates
/// so libFuzzer records it as a real bug.
/// </para>
/// </summary>
public static class SettingsTarget
{
    public static void Run(ReadOnlySpan<byte> data)
    {
        if (data.Length > FuzzLimits.MaxInputLength)
        {
            return;
        }

        string json = Encoding.UTF8.GetString(data);

        try
        {
            SettingsManager.DeserializeSettings(json);
        }
        catch (JsonException)
        {
            // Malformed JSON is the expected result of arbitrary bytes — not a bug.
        }
    }
}
