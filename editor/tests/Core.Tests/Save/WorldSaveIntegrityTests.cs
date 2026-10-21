using System.Globalization;
using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What protects a world's objects: every object reads and writes back exactly, and the short position of the game's terrain
/// objects is kept while it fits.
/// </summary>
public class WorldSaveIntegrityTests
{
    [Fact]
    public void EveryObjectOfTheSaveReadsAndWritesBackExactly()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Assert.True(world.ObjectRefs.Count > 100);
        for (int id = 0; id < world.ObjectRefs.Count; id++)
        {
            byte[] bytes = world.ObjectBytes(id);
            byte[] again = ZdoData.Parse(bytes).Serialize();
            int at = bytes.AsSpan().CommonPrefixLength(again);
            Assert.True(bytes.AsSpan().SequenceEqual(again), $"object {id} ({PrefabName(world.ObjectRefs[id].Prefab)}) changes when written back: {bytes.Length} bytes, then {again.Length}, first difference at {at}: {Convert.ToHexString(bytes.AsSpan(Math.Max(0, at - 8), Math.Min(24, bytes.Length - Math.Max(0, at - 8))))} vs {Convert.ToHexString(again.AsSpan(Math.Max(0, at - 8), Math.Min(24, again.Length - Math.Max(0, at - 8))))}");
        }
    }

    [Fact]
    public void EveryObjectOfALiveSnapshotReadsAndWritesBackExactly()
    {
        var live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        for (int id = 0; id < live.ObjectRefs.Count; id++)
        {
            byte[] bytes = live.ObjectBytes(id);
            Assert.True(bytes.AsSpan().SequenceEqual(ZdoData.Parse(bytes).Serialize()), $"live object {id} changes when written back");
        }
    }

    /// <summary>
    /// A prefab's name for messages, or its hash when unknown.
    /// </summary>
    private static string PrefabName(int hash) => TerrainEditor.Terrain.PrefabCatalog.DisplayName(hash) ?? hash.ToString(CultureInfo.InvariantCulture);

    // The game saves some objects (the terrain compiler) with a short position: kept while it fits.
    [Fact]
    public void AShortPositionIsKeptUntilItNoLongerFits()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        int id = world.ObjectRefs.FindIndex(o => o.IsTerrain);
        byte[] bytes = world.ObjectBytes(id);
        var z = ZdoData.Parse(bytes);
        Assert.True(z.SmallPosition);
        Assert.Equal(0f, z.Position.Y);
        // Moved by whole metres: still the short form, 8 bytes smaller than the full one.
        z.Position += new Vector3(64, 0, -64);
        byte[] moved = z.Serialize();
        Assert.Equal(bytes.Length, moved.Length);
        Assert.Equal(z.Position, ZdoData.Parse(moved).Position);
        // Off the whole metre, or lifted: the full form, read back exactly.
        foreach (var p in new[] { new Vector3(10.5f, 0, 3), new Vector3(10, 2, 3), new Vector3(40000, 0, 3) })
        {
            z.Position = p;
            byte[] full = z.Serialize();
            Assert.Equal(bytes.Length + 8, full.Length);
            var back = ZdoData.Parse(full);
            Assert.False(back.SmallPosition);
            Assert.Equal(p, back.Position);
        }
    }
}
