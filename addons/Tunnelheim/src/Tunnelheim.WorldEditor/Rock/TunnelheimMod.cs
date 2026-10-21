
namespace Tunnelheim.WorldEditor;

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
    public static readonly int RecordPrefab = Hash.Of(RecordPrefabName);

    /// <summary>The checkpoint's prefab hash.</summary>
    public static readonly int CheckpointPrefab = Hash.Of(CheckpointPrefabName);

    /// <summary>The record's data (byte array): the gzipped <see cref="ColumnRecord"/>.</summary>
    public const string RecordKeyName = "Tunneling_Digs";

    /// <summary>The checkpoint's data (byte array): the gzipped <see cref="ColumnCheckpoint"/>.</summary>
    public const string CheckpointKeyName = "Tunneling_SnapData";

    /// <summary>The checkpoint's id (int), which its column's record names.</summary>
    public const string CheckpointIdKeyName = "Tunneling_SnapId";

    /// <summary>The record's data key hash.</summary>
    public static readonly int RecordKey = Hash.Of(RecordKeyName);

    /// <summary>The checkpoint's data key hash.</summary>
    public static readonly int CheckpointKey = Hash.Of(CheckpointKeyName);

    /// <summary>The checkpoint's id key hash.</summary>
    public static readonly int CheckpointIdKey = Hash.Of(CheckpointIdKeyName);

    /// <summary>Whether a prefab is one of the mod's data objects (a record or a checkpoint): no model, never shown.</summary>
    /// <param name="prefab">The prefab hash.</param>
    public static bool IsData(int prefab) => prefab == RecordPrefab || prefab == CheckpointPrefab;
}
