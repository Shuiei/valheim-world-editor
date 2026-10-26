using TerrainEditor.Save;

/// <summary>The inspect check: the chunk mapping and the layout of the terrain objects of a world.</summary>
internal static class InspectCheck
{
    /// <summary>Checks that each terrain object sits in the chunk file the mapping names and prints its layout; returns the exit code.</summary>
    /// <param name="world">The world to inspect.</param>
    internal static int Run(WorldSave world)
    {
        // Checks used while building the writer: chunk mapping and the layout of terrain objects.
        int ok = 0, bad = 0;
        foreach (TerrainZone z in world.TerrainZones)
        {
            var (sx, sz) = ChunkMath.SectorOf(z.Center.X, z.Center.Z);
            ChunkFile? expected = ChunkMath.Find(world.Chunks, sx, sz, world.GridWidth);
            if (expected == z.Source!.File)
            {
                ok++;
            }
            else { bad++; Console.WriteLine($"  zone ({z.ZoneX},{z.ZoneZ}) is in {z.Source.File.FileName}, mapping says {expected?.FileName ?? "none"}"); }
        }
        Console.WriteLine($"Chunk mapping: {ok} terrain objects in the expected file, {bad} elsewhere.");
        Console.WriteLine($"Locations: {world.Locations.Count}; chunk files: {string.Join(", ", world.Chunks.Select(c => $"{c.FileName}({c.Count}/{c.IndexCount})"))}");
        foreach (TerrainZone z in world.TerrainZones.Take(3))
        {
            var src = z.Source!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(world.Directory, src.File.FileName));
            Console.WriteLine($"  terrain object zone ({z.ZoneX},{z.ZoneZ}) prefab {z.Prefab} flags 0x{src.Flags:x4} pos ({z.Center.X},{z.Center.Y},{z.Center.Z}) object {src.End - src.Start} B, TCData {src.DataEnd - src.DataStart - 4} B");
            Console.WriteLine($"    header bytes: {Convert.ToHexString(bytes, (int)src.Start, (int)(src.DataStart - src.Start))}  tail bytes after data: {src.End - src.DataEnd}");
        }
        return 0;
    }
}
