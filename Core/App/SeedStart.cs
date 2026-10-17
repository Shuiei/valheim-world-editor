using ValheimGen;
using static TerrainEditor.App.SeedPreview;

namespace TerrainEditor.App;

/// <summary>Where a biome map's start is, and what lies around it.</summary>
internal static class SeedStart
{
    /// <summary>The start where the game is not told: the Meadows cell nearest the middle.</summary>
    /// <param name="biome">Each cell's biome byte.</param>
    /// <param name="size">Cells on a side.</param>
    /// <param name="cell">A cell's side in metres.</param>
    /// <returns>The start's cell (-1 when there is no Meadows cell, with the origin as position).</returns>
    internal static (int Cell, (float X, float Z) Position) MiddleCell(byte[] biome, int size, float cell)
    {
        int meadows = Array.IndexOf(Biomes, Heightmap.Biome.Meadows);
        int startCell = -1;
        float best = float.MaxValue;
        for (int k = 0; k < biome.Length; k++)
        {
            if (biome[k] != meadows)
            {
                continue;
            }
            float x = -Radius + (k % size + 0.5f) * cell, z = -Radius + (k / size + 0.5f) * cell, d = x * x + z * z;
            if (d < best)
            {
                best = d;
                startCell = k;
            }
        }
        var start = startCell < 0 ? (0f, 0f) : (-Radius + (startCell % size + 0.5f) * cell, -Radius + (startCell / size + 0.5f) * cell);
        return (startCell, start);
    }

    /// <summary>The start at the game's start temple: its own position, and the nearest land cell for its landmass.</summary>
    /// <param name="biome">Each cell's biome byte.</param>
    /// <param name="size">Cells on a side.</param>
    /// <param name="cell">A cell's side in metres.</param>
    /// <param name="at">The temple's position.</param>
    /// <returns>The nearest land cell (-1 when there is none), with the temple as position.</returns>
    internal static (int Cell, (float X, float Z) Position) TempleCell(byte[] biome, int size, float cell, (float X, float Z) at)
    {
        int startCell = -1;
        float best = float.MaxValue;
        for (int k = 0; k < biome.Length; k++)
        {
            if (biome[k] == Ocean)
            {
                continue;
            }
            float dx = -Radius + (k % size + 0.5f) * cell - at.X, dz = -Radius + (k / size + 0.5f) * cell - at.Z, d = dx * dx + dz * dz;
            if (d < best)
            {
                best = d;
                startCell = k;
            }
        }
        return (startCell, (at.X, at.Z));
    }

    /// <summary>Around the start: the land near it, and the nearest of each biome and whether it is on the start's landmass.</summary>
    /// <param name="biome">Each cell's biome byte.</param>
    /// <param name="size">Cells on a side.</param>
    /// <param name="cell">A cell's side in metres.</param>
    /// <param name="start">The start's position.</param>
    /// <param name="mass">Each cell's landmass number (see <see cref="SeedLandmasses.Find"/>).</param>
    /// <param name="startMass">The start's landmass number (-1: none).</param>
    /// <returns>The cells within 400 m of the start, the land ones among them, and per biome the nearest distance and whether it is on the start's landmass.</returns>
    internal static (int Near, int NearLand, Dictionary<Heightmap.Biome, float> Distance, Dictionary<Heightmap.Biome, bool> Same) Distances(byte[] biome, int size, float cell, (float X, float Z) start, int[] mass, int startMass)
    {
        int near = 0, nearLand = 0;
        var distance = Biomes.ToDictionary(b => b, _ => float.PositiveInfinity);
        var same = Biomes.ToDictionary(b => b, _ => false);
        for (int k = 0; k < biome.Length; k++)
        {
            float x = -Radius + (k % size + 0.5f) * cell, z = -Radius + (k / size + 0.5f) * cell;
            float d = MathF.Sqrt((x - start.X) * (x - start.X) + (z - start.Z) * (z - start.Z));
            if (d <= 400 + cell / 2)
            {
                near++;
                nearLand += biome[k] != Ocean ? 1 : 0;
            }
            if (biome[k] == Ocean)
            {
                continue;
            }
            var b = Biomes[biome[k]];
            if (d < distance[b])
            {
                distance[b] = d;
                same[b] = mass[k] == startMass;
            }
        }
        return (near, nearLand, distance, same);
    }
}
