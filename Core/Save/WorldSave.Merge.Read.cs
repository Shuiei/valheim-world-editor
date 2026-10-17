namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Reads the game's objects that are new or changed (as one chunk of their bytes): a changed one replaces its object
    /// under its own id, a new one gets a new id; each is then listed in the result.
    /// </summary>
    /// <param name="fresh">The objects to read, with the id each replaces (-1 for a new one).</param>
    /// <param name="result">Receives the added and updated ids, the terrain zones and whether a ground shaper changed.</param>
    private void ReadFresh(List<(long User, uint Id, byte[] Bytes, int Replace)> fresh, Merged result)
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
}
