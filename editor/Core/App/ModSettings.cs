using System.Text;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// The mods' BepInEx settings files the running extensions' manifests ask for (their worldeditor.json's
/// "settingsFiles"; IEditorHost.Setting): read as a world or the Workshop opens from the first of the player's BepInEx
/// folders that has each, and for a live world again from the game's own, through its bridge.
/// </summary>
public static class ModSettings
{
    private static readonly IReadOnlyDictionary<(string Section, string Key), string> NoValues = new Dictionary<(string, string), string>();

    /// <summary>The settings read, by file name, swapped in whole.</summary>
    private static volatile IReadOnlyDictionary<string, IReadOnlyDictionary<(string Section, string Key), string>> _byFile =
        new Dictionary<string, IReadOnlyDictionary<(string, string), string>>();

    /// <summary>A setting as last read (IEditorHost.Setting); null when its file was not asked for or read, or has no such setting.</summary>
    /// <param name="file">The file's name in BepInEx/config.</param>
    /// <param name="section">Its section, as the heading names it without the brackets.</param>
    /// <param name="key">The setting's name.</param>
    public static string? Value(string file, string section, string key) =>
        _byFile.TryGetValue(file, out var values) && values.TryGetValue((section, key), out string? value) ? value : null;

    /// <summary>
    /// Reads each file from the first of the player's BepInEx folders that has it in its config folder (none: the mod's
    /// defaults apply). Never throws: a file that cannot be read gives no settings (logged).
    /// </summary>
    /// <param name="settings">The editor's settings (the BepInEx folders).</param>
    /// <param name="files">The files the running extensions ask for.</param>
    public static void Refresh(AppSettings settings, IReadOnlyCollection<string> files)
    {
        var configs = LocalGame.BepInExFolders(settings).Select(f => Path.Combine(f.BepInEx, "config")).ToList();
        _byFile = files.ToDictionary(f => f, f => Parse(TextOf(configs.Select(c => Path.Combine(c, f)).FirstOrDefault(File.Exists)) ?? ""), StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads each file again from the game's BepInEx/config, through its bridge (GET /config): the game's settings are the
    /// world's (a server's synced ones the players get). A file the game does not have gives no settings. A bridge older
    /// than <see cref="ExpandWorld.BridgeFilesVersion"/> leaves those of the local folders (<see cref="Refresh"/>), and a
    /// file the bridge fails to give keeps its local ones, said in the log. Never throws.
    /// </summary>
    /// <param name="live">The game's bridge.</param>
    /// <param name="files">The files the running extensions ask for.</param>
    public static async Task RefreshLive(LiveBridge live, IReadOnlyCollection<string> files)
    {
        if (files.Count == 0 || !await ServesFiles(live))
        {
            return;
        }
        Dictionary<string, IReadOnlyDictionary<(string, string), string>> byFile = new(_byFile, StringComparer.Ordinal);
        foreach (string file in files)
        {
            if (await LiveFile(live, file) is { } values)
            {
                byFile[file] = values;
            }
        }
        _byFile = byFile;
    }

    /// <summary>Whether the game's bridge serves its files (GET /plugins answers); false for an older one, or one that fails (logged).</summary>
    private static async Task<bool> ServesFiles(LiveBridge live)
    {
        try
        {
            if (await live.Plugins() != null)
            {
                return true;
            }
            Console.WriteLine($"Mods' settings: the game's WorldEditorBridge does not serve its files (needs {ExpandWorld.BridgeFilesVersion} or newer): the local BepInEx folders' are used.");
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException)
        {
            Log.Caught(e, "asking the game's bridge which plugins it runs (the local BepInEx folders' settings are used)");
        }
        return false;
    }

    /// <summary>A file's settings as the game has them (none when it has no such file); null when the bridge fails to give it (logged).</summary>
    private static async Task<IReadOnlyDictionary<(string Section, string Key), string>?> LiveFile(LiveBridge live, string file)
    {
        try
        {
            var values = await live.ConfigFile(file) is { } bytes ? Parse(Encoding.UTF8.GetString(bytes)) : NoValues;
            Console.WriteLine($"Mods' settings: {file} read from the game through WorldEditorBridge ({values.Count} settings).");
            return values;
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            Log.Caught(e, $"reading {file} through the game's bridge (the local BepInEx folders' settings are used)");
            return null;
        }
    }

    /// <summary>A BepInEx settings file's settings by section and name (the last of two the same wins).</summary>
    /// <param name="text">The file's text.</param>
    internal static IReadOnlyDictionary<(string Section, string Key), string> Parse(string text)
    {
        Dictionary<(string, string), string> values = [];
        foreach (var (section, key, value) in Entries(text))
        {
            values[(section, key)] = value;
        }
        return values;
    }

    /// <summary>
    /// A BepInEx settings file's "Key = value" lines in order, each with the section its heading names ("" before the
    /// first); comments and blank lines passed over.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static IEnumerable<(string Section, string Key, string Value)> Entries(string text)
    {
        string section = "";
        foreach (string line in text.TrimStart('﻿').Split('\n').Select(l => l.Trim()))
        {
            if (line.Length > 1 && line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
            }
            else if (line.Split('=', 2) is [string key, string value] && key.Length > 0 && key[0] is not ('#' or '['))
            {
                yield return (section, key.Trim(), value.Trim());
            }
        }
    }

    /// <summary>A settings file's text; null when there is no file or it cannot be read (said in the log: the mod's defaults then apply).</summary>
    /// <param name="path">The file; null for none.</param>
    internal static string? TextOf(string? path)
    {
        if (path == null)
        {
            return null;
        }
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"reading {Path.GetFileName(path)} (its settings are taken as the mod's defaults)");
            return null;
        }
    }
}
