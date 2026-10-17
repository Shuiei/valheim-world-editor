
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>What a <c>MergeLive</c> call changed.</summary>
    /// <param name="Added">Objects new to the editor (new ids).</param>
    /// <param name="Updated">Objects the game changed (data, place), read again under their own id.</param>
    /// <param name="Vanished">Objects gone from the game.</param>
    /// <param name="TerrainZones">Zones whose ground data came or went.</param>
    public sealed record Merged(List<int> Added, List<int> Updated, List<int> Vanished, HashSet<(int X, int Z)> TerrainZones)
    {
        /// <summary>Objects that shape the ground as the game runs came, went or changed.</summary>
        public bool Modifiers { get; set; }
    }

    /// <summary>
    /// Live: the zones' objects as the game has them now (ZDOID and bytes, as the plugin's /zone sends them), merged
    /// in. Null when the world's lists are being read elsewhere right now, or held (<see cref="Hold"/>): try again
    /// later.
    /// </summary>
    /// <param name="zones">The zones the objects were read from; objects of them the game lacks vanish.</param>
    /// <param name="objects">Every object the game has in those zones (a ZDOID listed twice counts once, its last bytes).</param>
    /// <param name="keep">
    /// Objects not to vanish though the game lacks them (the editor's own applied deletions: undo brings them back).
    /// </param>
    /// <param name="ours">ZDOIDs of objects the editor made itself (its new objects).</param>
    public Merged? MergeLive(IReadOnlyCollection<(int X, int Z)> zones, IReadOnlyList<(long User, uint Id, byte[] Bytes)> objects,
        Func<int, bool> keep, Func<(long User, uint Id), bool> ours)
    {
        if (!Monitor.TryEnter(Sync))
        {
            return null;
        }
        try
        {
            if (_holds > 0)
            {
                return null;
            }
            Index();
            var result = new Merged([], [], [], []);
            var seen = new HashSet<int>();
            var fresh = new List<(long User, uint Id, byte[] Bytes, int Replace)>();
            // Each ZDOID once: should the game list one twice, its last bytes stand (else it would be read twice, or
            // get two ids of which one never vanishes).
            var last = new Dictionary<(long User, uint Id), int>();
            for (int i = 0; i < objects.Count; i++)
            {
                last[(objects[i].User, objects[i].Id)] = i;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                var (user, zid, bytes) = objects[i];
                if (last[(user, zid)] != i)
                {
                    continue;
                }
                // A kept object whose game object is the editor's now (moved under its own ZDOID to stand for one of
                // the editor's new objects) is not read back in under its own id.
                if (_byLiveId!.TryGetValue((user, zid), out int id) && !(keep(id) && ours((user, zid))))
                {
                    seen.Add(id);
                    if (!SavedSpan(id).SequenceEqual(bytes))
                    {
                        fresh.Add((user, zid, bytes, id));
                    }
                }
                else if (!ours((user, zid)))
                {
                    fresh.Add((user, zid, bytes, -1));
                }
            }
            var gone = zones.SelectMany(z => _byZone!.TryGetValue(z, out var set) ? set : Enumerable.Empty<int>()).Where(id => !seen.Contains(id) && !keep(id)).ToList();
            // Out of the lists at once (one pass each): the vanished, and the changed ones read again below.
            var drop = gone.Concat(fresh.Where(f => f.Replace >= 0).Select(f => f.Replace)).ToHashSet();
            if (drop.Count > 0)
            {
                Drop(drop, result);
            }
            foreach (int id in gone)
            {
                Untrack(id);
                Vanished.Add(id);
                result.Vanished.Add(id);
            }
            if (fresh.Count > 0)
            {
                byte[] all = [.. fresh.SelectMany(f => f.Bytes)];
                ChunkFile file = new() { Chunk = 0, Size = 0, Version = 0, IndexCount = 0, WorldVersion = Chunks.Count > 0 ? Chunks[0].WorldVersion : 41, Count = fresh.Count, Length = all.Length, Bytes = all };
                using ValheimReader pkg = new(new MemoryStream(all));
                foreach (var (user, zid, _, replace) in fresh)
                {
                    if (replace >= 0)
                    {
                        Untrack(replace);
                    }
                    int zonesBefore = TerrainZones.Count;
                    int id = ReadZdo(pkg, file.WorldVersion, file, replace);
                    ObjectRefs[id].LiveId = (user, zid);
                    Track(id);
                    (replace >= 0 ? result.Updated : result.Added).Add(id);
                    if (replace < 0)
                    {
                        ObjectCount++;
                    }
                    if (TerrainZones.Count > zonesBefore)
                    {
                        var z = TerrainZones[^1];
                        result.TerrainZones.Add((z.ZoneX, z.ZoneZ));
                    }
                    result.Modifiers |= Shapes(ObjectRefs[id].Prefab);
                }
            }
            return result;
        }
        finally
        {
            Monitor.Exit(Sync);
        }
    }
}
