using static TerrainEditor.App.SeedPreview;

namespace TerrainEditor.App;

/// <summary>The landmasses of a biome map: land cells joined side by side.</summary>
internal static class SeedLandmasses
{
    /// <summary>A cell's four side neighbours, as column and row steps.</summary>
    private static readonly (int Di, int Dj)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Numbers the landmasses of a size x size biome map.</summary>
    /// <returns>Each cell's landmass number (-1 at sea) and the cell count of each landmass.</returns>
    internal static (int[] Mass, List<int> Sizes) Find(byte[] biome, int size)
    {
        // Landmasses: land cells joined side by side.
        int[] mass = new int[size * size];
        Array.Fill(mass, -1);
        var sizes = new List<int>();
        var queue = new Queue<int>();
        for (int seedCell = 0; seedCell < mass.Length; seedCell++)
        {
            if (biome[seedCell] == Ocean || mass[seedCell] >= 0)
            {
                continue;
            }
            int id = sizes.Count, n = 0;
            mass[seedCell] = id;
            queue.Enqueue(seedCell);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                n++;
                int ci = c % size, cj = c / size;
                foreach (var (di, dj) in Sides)
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= size || nj >= size)
                    {
                        continue;
                    }
                    int nk = nj * size + ni;
                    if (biome[nk] != Ocean && mass[nk] < 0)
                    {
                        mass[nk] = id;
                        queue.Enqueue(nk);
                    }
                }
            }
            sizes.Add(n);
        }
        return (mass, sizes);
    }
}
