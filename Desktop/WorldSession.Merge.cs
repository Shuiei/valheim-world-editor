using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary><see cref="MergeGame"/> for a read taken with no Apply live begun or ended since, under the apply lock.</summary>
    private Followed? Merge(Fetched f)
    {
        var deleted = Edits.Deleted;
        // Kept as the editor has them: its applied deletions (undo brings them back).
        var merged = World.MergeLive(f.Changed, f.Objects, LiveSync.IsDestroyed, LiveSync.IsOurs);
        if (merged == null)
        {
            return null;
        }
        foreach (var key in f.Changed)
        {
            _digests[key] = f.Digests[key];
        }
        // Gone from the game while deleted in the editor, not applied yet: nothing left to delete.
        Edits.SetDeleted([.. merged.Vanished.Where(deleted.Contains)], false);
        // The editor's new objects the game no longer has in the zones read.
        var changed = f.Changed.ToHashSet();
        var there = f.Objects.Select(o => (o.User, o.Id)).ToHashSet();
        var ours = LiveSync.NewObjects().Where(n => changed.Contains(Zone(n.Target.Position)) && !there.Contains((n.Target.User, n.Target.Id))).Select(n => n.Id).ToList();
        foreach (int id in ours)
        {
            LiveSync.Forget(id);
        }
        Edits.SetDeleted(ours, true);
        var ground = new List<(int, int)>();
        var kept = new List<(int, int)>();
        foreach (var (x, z) in merged.TerrainZones)
        {
            var zone = World.TerrainZones.FirstOrDefault(t => t.ZoneX == x && t.ZoneZ == z);
            switch (Edits.TakeGameGround(x, z, zone))
            {
                case EditStore.GameGround.Taken: ground.Add((x, z)); break;
                case EditStore.GameGround.Kept: kept.Add((x, z)); break;
                default:
                    break;
            }
        }
        ground.AddRange(Edits.TakeWaitingGround());
        if (merged.Modifiers)
        {
            // (With the editor's ground discs: those it applied are its own, not merged.)
            UseModifiers(new TerrainModifiers(Uplift.PlacedNow(World, Edits)));
        }
        return new Followed(merged, ours, ground, kept);
    }

    /// <summary>Zones whose changes of the editor's were undone since the game changed their ground: theirs now.</summary>
    public List<(int X, int Z)> TakeWaitingGround() => Edits.TakeWaitingGround();

    /// <summary>The zone a world position is in (zones are 64 m, centred on multiples of 64).</summary>
    private static (int, int) Zone(System.Numerics.Vector3 p) => ChunkMath.SectorOf(p.X, p.Z);

    /// <summary>Live: the world read again from the game (what players changed since); pending edits are dropped.</summary>
    public async Task Reload()
    {
        World = await Live!.LoadWorld();
        Edits.ResetFrom(World);
        UseModifiers(new TerrainModifiers(World));
        LiveSync.Reset();
        _digests.Clear();
        History = null;
    }

    /// <summary>
    /// Drops every pending change (offline: the world is read again from its files, and the history kept at the
    /// last save comes back).
    /// </summary>
    public void Discard()
    {
        if (!IsLive)
        {
            // Read first: a folder that cannot be read leaves the session as it was, its own saves still known.
            var saved = WorldSave.Load(World.Directory);
            saved.Rules = World.Rules;
            PruneSaves();
            World = saved;
        }
        Edits.ResetFrom(World);
        UseModifiers(new TerrainModifiers(World));
        LiveSync.Reset();
        _digests.Clear();
        History = null;
        Area = null;
        // Offline the world is as it was last saved: so is the history kept then.
        if (!IsLive)
        {
            _nextId = -1;
            RestoreHistory();
        }
    }
}
