using System.Text.Json;
using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

// A live world's rules: the mods' files as the game itself has them, read through its bridge.
public static partial class ExpandWorld
{
    /// <summary>The first WorldEditorBridge that serves the game's files (GET /plugins, /config, /world/meta).</summary>
    public const string BridgeFilesVersion = "1.54.0";

    /// <summary>Where a live game's settings and data files are copied to, laid out as a BepInEx folder (the data folder's live-bepinex).</summary>
    public static string LiveFolder => Path.Combine(AppSettings.DataDir, "live-bepinex");

    /// <summary>
    /// A live world's rules: the game's own values while the Expand World switch is off; else the mods the game runs
    /// and their settings and data files as its bridge serves them (copied to <see cref="LiveFolder"/>, laid out as a
    /// BepInEx folder, and read as a local one), with the world's keys from its file on the game's disk. A bridge older
    /// than <see cref="BridgeFilesVersion"/>, or one that fails, leaves the local BepInEx folders to answer
    /// (<see cref="Resolve(WorldSave, AppSettings)"/>), said in the log. The YamlDotNet library the data files need is the local folders':
    /// the bridge serves nothing from plugins.
    /// </summary>
    /// <param name="save">The world as read from the game (its starting global keys are replaced by its file's when the bridge serves it).</param>
    /// <param name="settings">The editor's settings.</param>
    /// <param name="live">The game's bridge.</param>
    public static async Task<WorldRules> ResolveLive(WorldSave save, AppSettings settings, LiveBridge live)
    {
        try
        {
            await TakeLiveKeys(save, live);
            if (!settings.ExpandWorld)
            {
                return await ResolveLocal(save, settings);
            }
            var plugins = await live.Plugins();
            if (plugins == null)
            {
                Console.WriteLine($"Expand World: the game's WorldEditorBridge does not serve its files (needs {BridgeFilesVersion} or newer): the local BepInEx folders are used.");
                return await ResolveLocal(save, settings);
            }
            var found = await FetchLive(live, plugins, settings);
            var data = ReadData(found);
            await FetchBlueprints(live, found, data);
            await NeedGameRules(data);
            return Resolve(save, Keys(save.StartingGlobalKeys), found, data);
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException or JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "reading the mods' files through the game's bridge (the local BepInEx folders are used)");
            return await ResolveLocal(save, settings);
        }
    }

    /// <summary>
    /// Takes the world's starting global keys from its file on the game's disk (GET /world/meta): the loaded list the
    /// snapshot carries has none of Expand World Size's, which the mod takes out. An older bridge, a game without a
    /// world file, or a bridge that cannot serve it (an error status, said in the log) leaves the snapshot's: the mods'
    /// files are still read through the bridge.
    /// </summary>
    private static async Task TakeLiveKeys(WorldSave save, LiveBridge live)
    {
        try
        {
            byte[]? meta = await live.WorldMeta();
            if (meta != null)
            {
                save.TakeKeys(WorldSave.ReadMetadata(new MemoryStream(meta)));
            }
        }
        catch (Exception e) when (e is InvalidOperationException or EndOfStreamException or InvalidDataException or IOException)
        {
            Log.Caught(e, "reading the world file through the game's bridge (its keys are left as the game loaded them)");
        }
    }

    /// <summary>
    /// Copies the mods' files the game has to <see cref="LiveFolder"/>: each installed mod's settings file, and Expand
    /// World Data's data folder (its yaml files, at any depth). Which mods are installed is what the game loaded.
    /// </summary>
    /// <param name="live">The game's bridge.</param>
    /// <param name="plugins">The plugins the game loaded.</param>
    /// <param name="settings">The editor's settings (where the YamlDotNet library is looked for).</param>
    private static async Task<Found> FetchLive(LiveBridge live, IReadOnlyList<LiveBridge.Plugin> plugins, AppSettings settings)
    {
        string root = LiveFolder, config = Path.Combine(root, "config");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
        Directory.CreateDirectory(config);
        bool size = plugins.Any(p => p.Id == SizeGuid), data = plugins.Any(p => p.Id == DataGuid), rivers = plugins.Any(p => p.Id == RiversGuid);
        int files = 0;
        long bytes = 0;
        foreach (var (installed, guid) in new[] { (size, SizeGuid), (data, DataGuid), (rivers, RiversGuid) })
        {
            if (installed && await live.ConfigFile(guid + ".cfg") is { } cfg)
            {
                await File.WriteAllBytesAsync(Path.Combine(config, guid + ".cfg"), cfg);
                files++;
                bytes += cfg.Length;
            }
        }
        if (data)
        {
            (int n, long b) = await CopyFolder(live, DataFolder, Path.Combine(config, DataFolder), name => name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase));
            files += n;
            bytes += b;
        }
        Console.WriteLine($"Expand World: {files} files ({bytes} bytes) of the game's read through WorldEditorBridge into {root}.");
        var found = Look(root, "the game's BepInEx folder, through WorldEditorBridge");
        return found with { Size = size, Data = data, Rivers = rivers, PluginsFolder = LocalPlugins(settings) };
    }

    /// <summary>
    /// Copies a folder under the game's BepInEx/config, with its subfolders, keeping the files <paramref name="keep"/>
    /// says; nothing when the game has no such folder. A file the bridge refuses goes to <paramref name="refused"/>
    /// when given (the others are still copied), else fails the copy.
    /// </summary>
    /// <returns>How many files were copied, and their bytes.</returns>
    private static async Task<(int Files, long Bytes)> CopyFolder(LiveBridge live, string path, string into, Func<string, bool> keep, Action<string, InvalidOperationException>? refused = null)
    {
        var entries = await live.ConfigFolder(path);
        if (entries == null)
        {
            return (0, 0);
        }
        Directory.CreateDirectory(into);
        int files = 0;
        long bytes = 0;
        foreach (var entry in entries)
        {
            string entryPath = path + "/" + entry.Name;
            if (entry.Folder)
            {
                (int n, long b) = await CopyFolder(live, entryPath, Path.Combine(into, entry.Name), keep, refused);
                files += n;
                bytes += b;
            }
            else if (keep(entry.Name) && await ConfigFileOr(live, entryPath, refused) is { } content)
            {
                await File.WriteAllBytesAsync(Path.Combine(into, entry.Name), content);
                files++;
                bytes += content.Length;
            }
        }
        return (files, bytes);
    }

    /// <summary>
    /// Copies the game's blueprint files (every .blueprint and .vbuild at any depth of its blueprint folder, where the
    /// mod looks for them) when the location or vegetation file is in use: a location's or a rule's prefab may name
    /// one, and a blueprint may hold others. One the bridge refuses (too large) is noted and left out, as the mod
    /// leaves out a blueprint it cannot read.
    /// </summary>
    private static async Task FetchBlueprints(LiveBridge live, Found found, DataFiles data)
    {
        if (data.Locations is not { Count: > 0 } && data.Vegetation is not { Count: > 0 })
        {
            return;
        }
        string folder = BlueprintFolderOf(data.Cfg);
        await CopyFolder(live, folder, Path.Combine(found.Folder, "config", folder), name => BlueprintExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase), (path, e) =>
        {
            Log.Caught(e, $"copying the blueprint {path} from the game (left out)");
            data.Notes.Add($"the blueprint {path} could not be read from the game: {e.Message}");
        });
    }

    /// <summary>A file under the game's BepInEx/config; null when <paramref name="refused"/> takes the bridge's refusal of it.</summary>
    private static async Task<byte[]?> ConfigFileOr(LiveBridge live, string path, Action<string, InvalidOperationException>? refused)
    {
        if (refused == null)
        {
            return await live.ConfigFile(path);
        }
        try
        {
            return await live.ConfigFile(path);
        }
        catch (InvalidOperationException e)
        {
            refused(path, e);
            return null;
        }
    }

    /// <summary>
    /// The local plugins folder the YamlDotNet library is taken from for a live game's data files: the first BepInEx
    /// folder the editor knows (the Expand World folder in Settings first) that has it; without one, the first folder
    /// known, so the log names a real place; without any, the live folder's own (which has none).
    /// </summary>
    private static string LocalPlugins(AppSettings settings)
    {
        List<string> candidates = [];
        if (!string.IsNullOrWhiteSpace(settings.ExpandWorldFolder))
        {
            candidates.Add(Path.Combine(settings.ExpandWorldFolder, "plugins"));
        }
        candidates.AddRange(LocalGame.BepInExFolders(settings).Select(f => Path.Combine(f.BepInEx, "plugins")));
        return candidates.FirstOrDefault(p => YamlReader.FindDll(p) != null) ?? candidates.FirstOrDefault() ?? Path.Combine(LiveFolder, "plugins");
    }
}
