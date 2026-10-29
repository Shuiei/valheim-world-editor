using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>What a save will change, planned before any file is written, and what the write leaves behind.</summary>
    /// <param name="changed">The zone edits to save (those of reset ground left out).</param>
    /// <param name="groundReset">Zones whose ground is reset.</param>
    /// <param name="skipped">The edits that could not be saved, each with the reason.</param>
    internal sealed class Planned(IReadOnlyList<ZoneEdit> changed, HashSet<(int, int)> groundReset, List<string> skipped)
    {
        /// <summary>The zone edits to save.</summary>
        public IReadOnlyList<ZoneEdit> Changed { get; } = changed;
        /// <summary>Zones whose ground is reset.</summary>
        public HashSet<(int, int)> GroundReset { get; } = groundReset;
        /// <summary>The edits that could not be saved, each with the reason.</summary>
        public List<string> Skipped { get; } = skipped;
        /// <summary>Objects to leave out, by the chunk file they are in and their start.</summary>
        public Dictionary<ChunkFile, HashSet<long>> Removals { get; } = [];
        /// <summary>How many objects the deletions take out.</summary>
        public int DeletedCount { get; set; }
        /// <summary>How many objects are left out in all (deletions and zone resets).</summary>
        public int Removed { get; set; }
        /// <summary>Terrain objects with new ground data, by chunk file and object start.</summary>
        public Dictionary<ChunkFile, Dictionary<long, (ZdoLocation Loc, byte[] Data)>> Patches { get; } = [];
        /// <summary>The zones that could not be saved.</summary>
        public HashSet<(int, int)> NotSaved { get; } = [];
        /// <summary>New objects to append, by chunk file.</summary>
        public Dictionary<ChunkFile, List<byte[]>> Additions { get; } = [];
        /// <summary>Zones whose existing terrain objects get new ground data.</summary>
        public int Written { get; set; }
        /// <summary>Zones that get a new terrain object.</summary>
        public int Created { get; set; }
        /// <summary>New objects planned.</summary>
        public int AddedCount { get; set; }
        /// <summary>The files the write has listed so far.</summary>
        public List<string> NewFiles { get; } = [];
        /// <summary>The new save as read back to check it.</summary>
        public WorldSave? Read { get; set; }
        /// <summary>The chunk files the write replaced, with their new version and object count.</summary>
        public Dictionary<ChunkFile, (uint Version, int Count)> Updated { get; } = [];
        /// <summary>The save number written.</summary>
        public int NewNumber { get; set; }
    }

    /// <summary>Works out which objects the save takes out: the deleted ones, then those cleared by zone resets.</summary>
    internal static void PlanRemovals(WorldSave world, IReadOnlyCollection<int> deleted, IReadOnlyList<ZoneReset> resets, Planned plan)
    {
        void Remove(ObjectRef o) => (plan.Removals.TryGetValue(o.File, out var set) ? set : plan.Removals[o.File] = []).Add(o.Start);
        foreach (int id in deleted)
        {
            if (id >= 0 && id < world.ObjectRefs.Count)
            {
                Remove(world.ObjectRefs[id]);
            }
        }
        // Objects the deletions take out: ids past the list and repeated ids take out none.
        plan.DeletedCount = plan.Removals.Values.Sum(s => s.Count);
        Dictionary<(int, int), ZoneReset> resetByZone = resets.ToDictionary(r => (r.X, r.Z));
        foreach (ObjectRef o in world.ObjectRefs)
        {
            // Players' tombstones (with what they carried) are never cleared; what a mod keeps of its buildings' in its
            // own data objects (a mod's data holders) stays with the buildings.
            if (resetByZone.TryGetValue(o.Zone, out ZoneReset? r) && !(r.KeepBuildings && (o.IsPiece || App.ModManifests.IsData(o.Prefab)))
                && !(o.IsTerrain && !r.Ground) && o.Prefab != TombstonePrefab)
            {
                Remove(o);
            }
        }
        plan.Removed = plan.Removals.Values.Sum(s => s.Count);
    }

    /// <summary>Plans the new objects: a copy of the first object of the same prefab, in the chunk file of their zone.</summary>
    internal static void PlanNewObjects(WorldSave world, Planned plan, IReadOnlyList<NewObject> added, Func<int, int, ChunkFile?> findChunk)
    {
        Func<ObjectRef, byte[]> source = world.SourceReader();
        foreach (NewObject n in added)
        {
            var (zx, zz) = ChunkMath.SectorOf(n.Position.X, n.Position.Z);
            ChunkFile? target = findChunk(zx, zz);
            if (target == null)
            {
                plan.Skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: zone {zx}, {zz} {(ChunkMath.InWorld(zx, zz, world.GridWidth) ? "is not generated yet" : "is outside the world")}");
                continue;
            }
            byte[]? bytes = world.NewObjectBytes(n, source);
            if (bytes == null)
            {
                plan.Skipped.Add($"a new object at {n.Position.X:F0}, {n.Position.Z:F0}: unknown kind of object");
                continue;
            }
            (plan.Additions.TryGetValue(target, out var list) ? list : plan.Additions[target] = []).Add(bytes);
            plan.AddedCount++;
        }
    }
}
