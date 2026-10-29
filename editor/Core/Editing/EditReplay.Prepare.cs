using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// Works out how the pending edits go onto the game's save, and what clashes; nothing changes yet (the store and the
    /// disk are as they were).
    /// </summary>
    /// <param name="old">The save the world was read from (the edits' ids are its).</param>
    /// <param name="edits">The pending edits.</param>
    /// <param name="saved">What the editor's saves of the session wrote.</param>
    /// <param name="newer">The game's save.</param>
    public static EditReplay Prepare(WorldSave old, EditStore edits, Saved saved, WorldSave newer)
    {
        int version = edits.Version;
        var r = new EditReplay(old, newer, version);
        foreach (var n in saved.Added)
        {
            r._savedAdds.TryAdd(n.Id, n);
        }
        r.PlanZones(edits, saved);
        r.PlanObjects(edits.Deleted, edits.Added, edits.Gone, saved);
        r._resets.AddRange(edits.Resets);
        return r;
    }

    /// <summary>
    /// The zones the edits change since the save read (those changed and those the editor's save wrote): a zone the
    /// game left as the editor read it, or as the editor saved it, takes the edits as they are; another clashes.
    /// </summary>
    private void PlanZones(EditStore edits, Saved saved)
    {
        var read = Zones(_old);
        var game = Zones(Newer);
        foreach (var mine in edits.Copies(e => e.Changed || saved.Zones.Contains((e.ZoneX, e.ZoneZ))))
        {
            var key = (mine.ZoneX, mine.ZoneZ);
            var was = Ground(read, key);
            var now = Ground(game, key);
            var last = saved.Zones.Contains(key) ? edits.BaselineOf(key.ZoneX, key.ZoneZ) ?? new ZoneEdit(key.ZoneX, key.ZoneZ) : was;
            if (now.SameGround(was) || now.SameGround(last))
            {
                _zones.Add(mine);
            }
            else
            {
                _zoneClashes.Add((mine, was, last, now));
                _clashes.Add(new Clash(ReplayClash.Ground, 0, new Vector3(key.ZoneX * 64f, 0f, key.ZoneZ * 64f), false));
            }
        }
    }

    /// <summary>A save's zones with terrain data, by zone; of two in one zone the first, as the edit store takes them.</summary>
    private static Dictionary<(int, int), TerrainZone> Zones(WorldSave world)
    {
        var zones = new Dictionary<(int, int), TerrainZone>();
        foreach (var z in world.TerrainZones)
        {
            if (EditStore.Of(z) != null)
            {
                zones.TryAdd((z.ZoneX, z.ZoneZ), z);
            }
        }
        return zones;
    }

    /// <summary>A zone's ground in a save as an edit (no changes when it has no terrain data).</summary>
    private static ZoneEdit Ground(Dictionary<(int, int), TerrainZone> zones, (int X, int Z) key) =>
        zones.TryGetValue(key, out var z) && EditStore.Of(z) is { } e ? e : new ZoneEdit(key.X, key.Z);
}
