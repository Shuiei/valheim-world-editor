
namespace TerrainEditor.Desktop;

/// <summary>
/// Walls on a rectangle (ClaudeBuilder.Walls.cs), with their doors.
/// </summary>
public static partial class ClaudeBuilder
{
    /// <summary>
    /// Walls on the rectangle's edges from bottom up to height (wood: 2 m rows; stone: 1 m rows), with
    /// doors (a 2 m wood door in place of the bottom of the 2 m of wall at that point of that side).
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="bottom">The height the walls stand on, in metres.</param>
    /// <param name="height">How tall, in metres (rounded to whole rows, at least one).</param>
    /// <param name="material">"wood" or "stone" (any case); anything else throws <see cref="ArgumentException"/>.</param>
    /// <param name="doors">The doors; a side that is not north, south, east or west throws.</param>
    /// <exception cref="ArgumentException">Also when it would be more than <see cref="MaxPieces"/> pieces.</exception>
    public static List<ClaudeTools.PieceSpec> Walls(float x0, float z0, float x1, float z1, float bottom, float height, string material, IReadOnlyList<Door> doors)
    {
        (float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
        (string prefab, float row) = WallKind(material);
        CheckWalls(x0, z0, x1, z1, height, material);
        int rows = (int)Rows(height, row);
        var info = Info(prefab);
        var door = Info("wood_door");
        var list = new List<ClaudeTools.PieceSpec>();
        var sides = new (string Name, bool AlongX, float Fixed, List<float> Spots)[]
        {
            ("south", true, lz, Across(lx, hx)), ("north", true, hz, Across(lx, hx)),
            ("west", false, lx, Across(lz, hz)), ("east", false, hx, Across(lz, hz)),
        };
        foreach (var d in doors)
        {
            if (!sides.Any(s => s.Name.Equals(d.Side, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"A door's side is north, south, east or west, not \"{d.Side}\".");
            }
        }
        // Each door in the one 2 m of wall nearest its point.
        var doorAt = new HashSet<(string, float)>();
        foreach (var d in doors)
        {
            var (name, alongX, @fixed, spots) = sides.First(s => s.Name.Equals(d.Side, StringComparison.OrdinalIgnoreCase));
            doorAt.Add((name, spots.OrderBy(sp => MathF.Abs(sp - d.At)).First()));
        }
        foreach (var (name, alongX, fixedAt, spots) in sides)
        {
            foreach (float spot in spots)
            {
                float x = alongX ? spot : fixedAt, z = alongX ? fixedAt : spot, yaw = alongX ? 0 : 90;
                bool hasDoor = doorAt.Contains((name, spot));
                int skip = 0;
                if (hasDoor)
                {
                    list.Add(P("wood_door", x, bottom - door.MinY, z, yaw));
                    skip = (int)MathF.Round(2 / row);
                }
                for (int r = skip; r < rows; r++)
                {
                    list.Add(P(prefab, x, bottom + r * row - info.MinY, z, yaw));
                }
            }
        }
        return list;
    }
}
