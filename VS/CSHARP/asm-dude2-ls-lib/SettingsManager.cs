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

namespace AsmDude2LS;

using System;
using System.IO;
using System.Text.Json;
using AsmTools;

/// <summary>
/// Manages AsmDude2 user settings stored in %APPDATA%\AsmDude2\settings.json.
/// Reads settings on startup and watches for file changes.
/// </summary>
public sealed class SettingsManager : IDisposable
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AsmDude2");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public static string SettingsFilePath => SettingsFile;

    private FileSystemWatcher? watcher;
    private DateTime lastWriteTime;

    /// <summary>Fired when the user edits and saves settings.json.</summary>
    public event Action<AsmLanguageServerOptions>? SettingsChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new ColorJsonConverter() },
    };

    /// <summary>
    /// Reads settings.json and merges over the provided defaults.
    /// If the file doesn't exist, creates it from defaults so the user can edit it.
    /// </summary>
    public AsmLanguageServerOptions LoadSettings(AsmLanguageServerOptions defaults)
    {
        try
        {
            if (!File.Exists(SettingsFile))
            {
                WriteDefaults(defaults);
                AsmDudeLog.Info($"SettingsManager: created default settings at {SettingsFile}");
                return defaults;
            }

            string json = File.ReadAllText(SettingsFile);
            var loaded = JsonSerializer.Deserialize<AsmLanguageServerOptions>(json, JsonOptions);
            if (loaded != null)
            {
                AsmDudeLog.Info($"SettingsManager: loaded user settings from {SettingsFile}");
                return loaded;
            }
        }
        catch (Exception ex)
        {
            AsmDudeLog.Error($"SettingsManager: failed to load settings: {ex.Message}; using defaults");
        }

        return defaults;
    }

    /// <summary>Start watching settings.json for changes.</summary>
    public void StartWatching()
    {
        try
        {
            if (!Directory.Exists(SettingsDir)) return;

            this.watcher = new FileSystemWatcher(SettingsDir, "settings.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            this.watcher.Changed += this.OnFileChanged;
            AsmDudeLog.Info("SettingsManager: watching for settings changes");
        }
        catch (Exception ex)
        {
            AsmDudeLog.Error($"SettingsManager: failed to start file watcher: {ex.Message}");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: FileSystemWatcher fires multiple events per save
        try
        {
            var writeTime = File.GetLastWriteTimeUtc(SettingsFile);
            if (writeTime == this.lastWriteTime) return;
            this.lastWriteTime = writeTime;

            // Small delay to let the editor finish writing
            System.Threading.Thread.Sleep(200);

            string json = File.ReadAllText(SettingsFile);
            var options = JsonSerializer.Deserialize<AsmLanguageServerOptions>(json, JsonOptions);
            if (options != null)
            {
                AsmDudeLog.Info("SettingsManager: settings changed, reloading");
                this.SettingsChanged?.Invoke(options);
            }
        }
        catch (Exception ex)
        {
            AsmDudeLog.Error($"SettingsManager: failed to reload settings: {ex.Message}");
        }
    }

    private static void WriteDefaults(AsmLanguageServerOptions defaults)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(defaults, JsonOptions);

            // Prepend a comment header (JSON doesn't support comments, but many editors tolerate it)
            string header = """
                // AsmDude2 Settings
                // Edit this file to configure AsmDude2. Changes are applied automatically.
                // Delete this file to reset all settings to defaults.
                //
                // Color values use the format: { "R": 255, "G": 128, "B": 0, "A": 255 }
                // Boolean values: true / false
                // Documentation URL: set AsmDoc_Url to your preferred wiki
                //

                """;
            File.WriteAllText(SettingsFile, header + json);
        }
        catch (Exception ex)
        {
            AsmDudeLog.Error($"SettingsManager: failed to write defaults: {ex.Message}");
        }
    }

    public void Dispose()
    {
        this.watcher?.Dispose();
    }
}
