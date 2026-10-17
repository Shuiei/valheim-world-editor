using static TerrainEditor.App.SeedPreview;

namespace TerrainEditor.App;

/// <summary>
/// The stats of a biome map of the whole world, as the New world page shows them: the share of land,
/// the main landmass, the start's landmass and how far each biome is from the start.
/// </summary>
internal static class SeedMeasure
{
    /// <summary>The stats of a size x size biome map of the whole world (Nearest and TempleStart left unset).</summary>
    /// <param name="biome">Each cell's biome byte, as <see cref="Sample"/> makes them.</param>
    /// <param name="size">Cells on a side.</param>
    /// <param name="cell">A cell's side in metres.</param>
    /// <param name="startAt">Where the game puts its start temple (null: the Meadows cell nearest the middle).</param>
    internal static Stats Measure(byte[] biome, int size, float cell, (float X, float Z)? startAt = null)
    {
        var (inside, land, counts) = CountLand(biome, size, cell);
        var (mass, sizes) = SeedLandmasses.Find(biome, size);
        var (startCell, start) = SeedStart.MiddleCell(biome, size, cell);
        if (startAt is { } at)
        {
            (startCell, start) = SeedStart.TempleCell(biome, size, cell, at);
        }
        int startMass = startCell < 0 ? -1 : mass[startCell];
        var (near, nearLand, distance, same) = SeedStart.Distances(biome, size, cell, start, mass, startMass);
        return new Stats(
            inside == 0 ? 0 : (float)land / inside,
            Biomes.Select((b, i) => (b, i)).ToDictionary(p => p.b, p => land == 0 ? 0f : (float)counts[p.i] / land),
            land == 0 || sizes.Count == 0 ? 0 : (float)sizes.Max() / land,
            start,
            near == 0 ? 0 : (float)nearLand / near,
            land == 0 || startMass < 0 ? 0 : (float)sizes[startMass] / land,
            distance,
            same);
    }

    /// <summary>Counts the cells inside the world's circle, and the land ones, and the land of each biome.</summary>
    /// <returns>The cells inside the world, the land cells inside it, and the land cells of each biome (index in Biomes).</returns>
    private static (int Inside, int Land, int[] Counts) CountLand(byte[] biome, int size, float cell)
    {
        int inside = 0, land = 0;
        int[] counts = new int[Biomes.Length];
        for (int j = 0; j < size; j++)
        {
            for (int i = 0; i < size; i++)
            {
                float x = -Radius + (i + 0.5f) * cell, z = -Radius + (j + 0.5f) * cell;
                if (x * x + z * z > Radius * Radius)
                {
                    continue;
                }
                inside++;
                if (biome[j * size + i] != Ocean)
                {
                    land++;
                    counts[biome[j * size + i]]++;
                }
            }
        }
        return (inside, land, counts);
    }
}
