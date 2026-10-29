using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class Growth
{
    /// <summary>
    /// What stands in the area within the farthest test's reach of the kept spots: each thing's position, its prefab's
    /// name and its kind.
    /// </summary>
    /// <param name="s">The area whose things are read, under their lock.</param>
    /// <param name="kept">The spots kept so far; only what stands near them is read.</param>
    /// <param name="nameOf">The prefab name of a prefab hash, null when unknown.</param>
    /// <returns>The things near the kept spots, in the order the area lists them.</returns>
    internal static List<(float X, float Z, string? Name, ObjectKind Kind)> StandingNear(WorldScene s, List<Regrow.Spot> kept, Func<int, string?> nameOf)
    {
        float minX = kept.Min(o => o.X) - (Farthest + 1), maxX = kept.Max(o => o.X) + (Farthest + 1);
        float minZ = kept.Min(o => o.Z) - (Farthest + 1), maxZ = kept.Max(o => o.Z) + (Farthest + 1);
        List<(float X, float Z, string? Name, ObjectKind Kind)> standing = [];
        lock (s.Things)
        {
            foreach (var t in s.Things)
            {
                if (t.Gone || !(t.Position.X > minX && t.Position.X < maxX && t.Position.Z > minZ && t.Position.Z < maxZ))
                {
                    continue;
                }
                string? name = nameOf(t.Prefab);
                standing.Add((t.Position.X, t.Position.Z, name, ObjectKinds.Of(name, t.Piece, t.Tamed)));
            }
        }
        return standing;
    }
}
