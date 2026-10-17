using System.Numerics;

namespace TerrainEditor.Desktop;

// The spacing check: the objects that stand, by grid cell.
public sealed partial class PlaceTool
{
    /// <summary>
    /// A spatial hash of points (grid positions), to test quickly whether a spot is the spacing away from all of them.
    /// </summary>
    internal sealed class Hash
    {
        /// <summary>The size of a hash cell, in metres.</summary>
        public readonly float Cell;
        /// <summary>The points in each cell.</summary>
        private readonly Dictionary<(int, int), List<Vector2>> _map = [];
        /// <summary>An empty hash with cells this many metres wide.</summary>
        public Hash(float cell) => Cell = cell;
        /// <summary>Adds a point.</summary>
        public void Add(Vector2 p)
        {
            var k = ((int)MathF.Floor(p.X / Cell), (int)MathF.Floor(p.Y / Cell));
            (_map.TryGetValue(k, out var l) ? l : _map[k] = []).Add(p);
        }
        /// <summary>Whether no point is closer than <c>spacing</c> to <c>p</c>.</summary>
        public bool Free(Vector2 p, float spacing)
        {
            int cx = (int)MathF.Floor(p.X / Cell), cz = (int)MathF.Floor(p.Y / Cell), n = Math.Max(1, (int)MathF.Ceiling(spacing / Cell));
            for (int dz = -n; dz <= n; dz++)
            {
                for (int dx = -n; dx <= n; dx++)
                {
                    if (_map.TryGetValue((cx + dx, cz + dz), out var l) && l.Any(q => Vector2.Distance(q, p) < spacing))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
