using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world writer tests' helpers: the files of a world folder, the zones a ground edit can be made in, and the ground edits.
/// </summary>
public static class WorldWriterTestHelpers
{
    /// <summary>
    /// Every file of the folder, by name.
    /// </summary>
    public static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

    /// <summary>
    /// Asserts the folder holds the same files with the same bytes as before.
    /// </summary>
    public static void AssertSame(Dictionary<string, byte[]> before, string dir)
    {
        var after = Files(dir);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (name, bytes) in before)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"{name} changed");
        }
    }

    /// <summary>
    /// A zone that has world data (a chunk file) but no ground edits yet.
    /// </summary>
    public static (int X, int Z) FreshZone(WorldSave world) => FreshZones(world).First();

    /// <summary>
    /// Zones that have world data (a chunk file) but no ground edits yet, nearest the first edited zone first.
    /// </summary>
    public static IEnumerable<(int X, int Z)> FreshZones(WorldSave world)
    {
        var t = world.TerrainZones[0];
        for (int r = 1; r < 6; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = t.ZoneX + dx, z = t.ZoneZ + dz;
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == r && ChunkMath.Find(world.Chunks, x, z) != null && world.TerrainZones.All(q => q.ZoneX != x || q.ZoneZ != z))
                    {
                        yield return (x, z);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A ground edit of this zone raising these points by this many metres.
    /// </summary>
    public static ZoneEdit Raised(int zx, int zz, float by, params int[] points)
    {
        var e = new ZoneEdit(zx, zz);
        foreach (int p in points)
        {
            e.Modified[p] = true;
            e.Level[p] = by;
        }
        return e;
    }
}
