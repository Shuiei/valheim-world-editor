using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Tunnelheim;

/// <summary>
/// Tunnelheim, the mod that digs real tunnels with the pickaxe (by Sam, plugin <see cref="PluginId"/>), as the editor
/// knows it: the objects it keeps a world's tunnels in, their data keys, its plugin file and its settings file. A save's
/// tunnels are read and drawn whether the mod is installed or not; the editor's Tunnel tool needs the mod.
/// </summary>
/// <remarks>Followed: Tunnelheim 1.0.0 (VoxelColumnSync, VoxelSnapshot, Plugin).</remarks>
public static class TunnelheimMod
{
    /// <summary>The mod's plugin id, which also names its settings file.</summary>
    public const string PluginId = "sam.valheim.tunneling";

    /// <summary>The mod's plugin file, under a BepInEx plugins folder.</summary>
    public const string DllName = "ValheimTunneling.dll";

    /// <summary>The prefab of a column's record (<see cref="ColumnRecord"/>).</summary>
    public const string RecordPrefabName = "TunnelColumnData";

    /// <summary>The prefab of a column's checkpoint (<see cref="ColumnCheckpoint"/>).</summary>
    public const string CheckpointPrefabName = "TunnelColumnSnapshot";

    /// <summary>The record's prefab hash.</summary>
    public static readonly int RecordPrefab = StableHash.Of(RecordPrefabName);

    /// <summary>The checkpoint's prefab hash.</summary>
    public static readonly int CheckpointPrefab = StableHash.Of(CheckpointPrefabName);

    /// <summary>The record's data (byte array): the gzipped <see cref="ColumnRecord"/>.</summary>
    public static readonly int RecordKey = StableHash.Of("Tunneling_Digs");

    /// <summary>The checkpoint's data (byte array): the gzipped <see cref="ColumnCheckpoint"/>.</summary>
    public static readonly int CheckpointKey = StableHash.Of("Tunneling_SnapData");

    /// <summary>The checkpoint's id (int), which its column's record names.</summary>
    public static readonly int CheckpointIdKey = StableHash.Of("Tunneling_SnapId");

    /// <summary>Whether a prefab is one of the mod's data objects (a record or a checkpoint): no model, never shown.</summary>
    /// <param name="prefab">The prefab hash.</param>
    public static bool IsData(int prefab) => prefab == RecordPrefab || prefab == CheckpointPrefab;

    /// <summary>
    /// The BepInEx folder with the mod's plugin DLL: the first folder the editor knows (<see
    /// cref="LocalGame.BepInExFolders"/>) that has it; null when none has.
    /// </summary>
    /// <param name="settings">The editor's settings.</param>
    public static string? Find(AppSettings settings) =>
        LocalGame.BepInExFolders(settings).Select(f => f.BepInEx).FirstOrDefault(dir => ExpandWorld.HasPlugin(Path.Combine(dir, "plugins"), DllName));

    /// <summary>
    /// The building height a BepInEx folder's settings give (<c>Building.MaxHeightAboveGround</c> in the mod's settings
    /// file): how far above the original ground rock may be added. The server's setting is the one a game uses; with
    /// no file, or no such setting, the mod's default.
    /// </summary>
    /// <param name="bepInEx">The BepInEx folder, or null for none.</param>
    public static float MaxBuildHeight(string? bepInEx)
    {
        string? path = bepInEx == null ? null : Path.Combine(bepInEx, "config", PluginId + ".cfg");
        if (path == null || !File.Exists(path))
        {
            return RockRules.DefaultMaxBuildHeight;
        }
        try
        {
            var cfg = ExpandWorld.ReadCfg(File.ReadAllText(path));
            return cfg.TryGetValue("MaxHeightAboveGround", out string? v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float h)
                ? h
                : RockRules.DefaultMaxBuildHeight;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"reading {Path.GetFileName(path)} (the building height is taken as the mod's default)");
            return RockRules.DefaultMaxBuildHeight;
        }
    }
}
