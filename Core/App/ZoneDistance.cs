namespace TerrainEditor.App;

/// <summary>
/// How far, in zones, each zone of the world lies from the nearest zone with pieces: a breadth-first walk out from
/// those zones over their 8 neighbours, so the distance is the larger of the x and z steps.
/// </summary>
internal sealed class ZoneDistance
{
    /// <summary>The largest zone coordinate kept (the world's, in zones, plus one).</summary>
    private readonly int _max;

    /// <summary>The side of the square of steps, in zones.</summary>
    private readonly int _size;

    /// <summary>The steps of each zone of the square (-1: not reached).</summary>
    private readonly int[] _steps;

    /// <summary>Walks out from the zones with pieces.</summary>
    /// <param name="withPieces">The zones holding pieces.</param>
    /// <param name="max">The largest zone coordinate kept, either sign.</param>
    internal ZoneDistance(IEnumerable<(int X, int Z)> withPieces, int max)
    {
        _max = max;
        _size = max * 2 + 1;
        _steps = new int[_size * _size];
        Array.Fill(_steps, -1);
        Queue<(int, int)> queue = new();
        foreach (var (x, z) in withPieces)
        {
            if (Math.Abs(x) <= max && Math.Abs(z) <= max)
            {
                _steps[(z + max) * _size + x + max] = 0;
                queue.Enqueue((x, z));
            }
        }
        while (queue.Count > 0)
        {
            var (x, z) = queue.Dequeue();
            int d = _steps[(z + max) * _size + x + max];
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (Math.Abs(nx) > max || Math.Abs(nz) > max)
                    {
                        continue;
                    }
                    int i = (nz + max) * _size + nx + max;
                    if (_steps[i] < 0)
                    {
                        _steps[i] = d + 1;
                        queue.Enqueue((nx, nz));
                    }
                }
            }
        }
    }

    /// <summary>The steps from a zone to the nearest zone with pieces.</summary>
    /// <param name="x">The zone's x.</param>
    /// <param name="z">The zone's z (inside the walked square).</param>
    /// <returns>The steps, or -1 when no zone with pieces is reached.</returns>
    internal int Of(int x, int z) => _steps[(z + _max) * _size + x + _max];
}
