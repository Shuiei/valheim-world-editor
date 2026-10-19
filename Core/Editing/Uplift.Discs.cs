using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Uplift
{
    /// <summary>The editor's discs as they are now: the world's (not deleted) and the new ones not saved yet.</summary>
    public static List<(int Id, Vector3 Position)> CurrentDiscs(WorldSave world, EditStore edits)
    {
        var deleted = edits.Deleted;
        var list = world.Discs.Where(d => !deleted.Contains(d.Id)).ToList();
        list.AddRange(edits.Added.Where(IsDiscObject).Select(n => (n.Id, n.Position)));
        return list;
    }

    /// <summary>Whether a new object is a location proxy of the disc location.</summary>
    private static bool IsDiscObject(NewObject n) =>
        n.Prefab == WorldSave.LocationProxyPrefab && n.Raw != null && ZdoData.Parse(n.Raw).IntList.Any(i => i.Key == LocationKey && i.Value == Disc);

    /// <summary>
    /// The world's placed objects with modifiers as the game will have them: the world's discs replaced
    /// by the current ones (and, for a plan, without the ones it replaces).
    /// </summary>
    public static List<PlacedObject> PlacedNow(WorldSave world, EditStore edits, IEnumerable<(int Id, Vector3 Position)>? without = null)
    {
        var discs = CurrentDiscs(world, edits);
        if (without != null)
        {
            var drop = without.Select(d => d.Id).ToHashSet();
            discs = [.. discs.Where(d => !drop.Contains(d.Id))];
        }
        var placed = world.Placed.Where(p => !IsDisc(p.Location)).ToList();
        placed.AddRange(discs.Select(d => DiscAt(d.Position)));
        return placed;
    }

    /// <summary>A disc at a place, as a placed location for TerrainModifiers.</summary>
    private static PlacedObject DiscAt(Vector3 p) => new(WorldSave.LocationProxyPrefab, Disc, p, Vector3.Zero, 0);

    /// <summary>The disc as a saved object: a location proxy (persistent, solid, like the game's own).</summary>
    public static byte[] DiscBytes(Vector3 position)
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, 0x100 | (2 << 10), position, Vector3.Zero, 0f));
        z.Set("ints", LocationKey, Disc.ToString(System.Globalization.CultureInfo.InvariantCulture));
        z.Set("ints", SeedKey, "0");
        return z.Serialize();
    }

    /// <summary>A zone the save has room for (a chunk file: what WorldWriter needs); the running game takes any.</summary>
    public static bool Generated(WorldSave world, int zx, int zz) => world.IsLive || ChunkMath.Find(world.Chunks, zx, zz, world.GridWidth) != null;

    /// <summary>How many zones beyond a lifted one its discs and their ground can reach: the margin of its block.</summary>
    private static readonly int ReachZones = (int)MathF.Ceiling((Spacing + DiscRadius + DiscReach + 8f) / 64f);
}
