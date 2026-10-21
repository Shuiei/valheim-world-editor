using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// An area of flat ground (IAreaView) holding the objects given, its biome colour the same everywhere; its biome, Deep
/// North, Ashlands and paint mask as set (Meadows, neither, untouched by default).
/// </summary>
internal sealed class FakeArea(float height, params IObjectView[] objects) : IAreaView
{
    public int BiomeValue { get; init; } = 1;

    public bool DeepNorth { get; init; }

    public bool Ashlands { get; init; }

    public uint Mask { get; init; } = 0xFF000000;

    /// <summary>The paint mask per whole metre, in place of <see cref="Mask"/> everywhere when set.</summary>
    public Func<int, int, uint>? MaskAt { get; init; }

    public float MinX => -64;

    public float MinZ => -64;

    public float MaxX => 64;

    public float MaxZ => 64;

    public IReadOnlyList<IObjectView> ObjectsOf(string prefab) => [.. objects.Where(o => o.Prefab == prefab)];

    public float GroundHeight(float x, float z) => height;

    public int Biome(float x, float z) => BiomeValue;

    public bool IsDeepNorth(float x, float z) => DeepNorth;

    public bool IsAshlands(float x, float z) => Ashlands;

    public uint BiomeColor(int x, int z) => 0x01020304;

    public uint GroundMask(int x, int z) => MaskAt?.Invoke(x, z) ?? Mask;
}

/// <summary>A saved object holding byte arrays and ints by key name (and nothing else).</summary>
internal class FakeObject(string prefab, Vector3 position, Dictionary<string, byte[]>? bytes = null, Dictionary<string, int>? ints = null) : IObjectView
{
    public string Prefab => prefab;

    public Vector3 Position => position;

    public Vector3 Rotation => Vector3.Zero;

    public Quaternion Orientation => Quaternion.Identity;

    public float Scale => 1;

    public bool TryGetInt(string key, out int value) => (ints ?? []).TryGetValue(key, out value);

    public bool TryGetLong(string key, out long value)
    {
        value = 0;
        return false;
    }

    public bool TryGetFloat(string key, out float value)
    {
        value = 0;
        return false;
    }

    public bool TryGetVector3(string key, out Vector3 value)
    {
        value = default;
        return false;
    }

    public bool TryGetQuaternion(string key, out Quaternion value)
    {
        value = default;
        return false;
    }

    public bool TryGetString(string key, out string value)
    {
        value = "";
        return false;
    }

    public bool TryGetBytes(string key, out byte[] value)
    {
        value = (bytes ?? []).TryGetValue(key, out byte[]? b) ? b : [];
        return bytes?.ContainsKey(key) == true;
    }

    /// <summary>A column's record object, at the middle of the column holding a point.</summary>
    public static FakeObject Record(ColumnRecord record, float x, float z)
    {
        var (cx, cz) = RockGrid.ColumnAt(x, z);
        return new(TunnelheimMod.RecordPrefabName, new Vector3((cx + 0.5f) * RockGrid.ColumnMetres, 0, (cz + 0.5f) * RockGrid.ColumnMetres),
            new Dictionary<string, byte[]> { [TunnelheimMod.RecordKeyName] = record.Encode() });
    }
}
