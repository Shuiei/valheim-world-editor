using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Growing the game's own vegetation (Regrow: the world generator's rules, on the ground as it is now)
/// over a rectangle of the world, for the Area tool's Regrow and the Mountain tool: the spots kept by
/// keep, not where something already stands (a tree still there is not doubled) or near buildings.
/// </summary>
public static class Growth
{
    /// <summary>
    /// The spots the game would grow in the rectangle, less those near what already stands (anything within 1 m,
    /// the same object within 3 m, buildings within 4 m) and those <paramref name="keep"/> refuses.
    /// </summary>
    /// <param name="session">The edit session whose world, edits and objects are used.</param>
    /// <param name="x0">The rectangle's lowest X, world metres.</param>
    /// <param name="z0">Its lowest Z, world metres.</param>
    /// <param name="x1">Its highest X, world metres.</param>
    /// <param name="z1">Its highest Z, world metres.</param>
    /// <param name="keep">Which spots to keep (the tool's own shape, for example).</param>
    /// <param name="nameOf">The prefab name of a prefab hash, null when unknown.</param>
    /// <param name="message">Shows a status line (progress, or why it cannot be done).</param>
    /// <param name="maxZones">The most zones the rectangle may cover (the work grows with them).</param>
    /// <returns>Null when it cannot be done (a message says why).</returns>
    public static async Task<List<Regrow.Spot>?> Spots(EditSession session, float x0, float z0, float x1, float z1, Func<Regrow.Spot, bool> keep, Func<int, string?> nameOf, Action<string> message, int maxZones = 16)
    {
        var s = session.Scene;
        if (s.Terrain is not { } terrain || s.World == null)
        {
            message("Regrow needs the world's generator (not available here).");
            return null;
        }
        static int Zone(float v) => (int)MathF.Floor((v + 32) / 64);
        int zx0 = Zone(x0), zx1 = Zone(x1), zz0 = Zone(z0), zz1 = Zone(z1);
        if ((zx1 - zx0 + 1) * (zz1 - zz0 + 1) > maxZones)
        {
            int side = (int)Math.Sqrt(maxZones);
            message($"Choose a smaller area: regrow works on up to {maxZones} zones ({side * 64} x {side * 64} m) at once.");
            return null;
        }
        message("Working out what the game grows here…");
        var world = s.World;
        var spots = await Task.Run(() => Regrow.Zones(terrain, session.Edits, world.Seed, zx0, zz0, zx1, zz1).Where(p => world.CanCreate(StableHash.Of(p.Name))).ToList());
        // The tool's own test first, here (it may read the window's state); the rest away from the window.
        var kept = spots.Where(keep).ToList();
        if (kept.Count == 0)
        {
            return kept;
        }
        // Only what stands within 4 m of a kept spot can stop it (the farthest test below); a metre more against
        // rounding.
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
        return await Task.Run(() =>
        {
            // In cells wider than the farthest test, so a spot looks only at its own cell and the eight around it.
            var cells = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < standing.Count; i++)
            {
                var key = (Cell(standing[i].X), Cell(standing[i].Z));
                if (!cells.TryGetValue(key, out var list))
                {
                    cells[key] = list = [];
                }
                list.Add(i);
            }
            bool Near(Regrow.Spot o, float d, Func<string?, ObjectKind, bool> test)
            {
                int cx = Cell(o.X), cz = Cell(o.Z);
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (cells.TryGetValue((cx + dx, cz + dz), out var list) && list.Exists(i =>
                        {
                            var (x, z, name, kind) = standing[i];
                            return MathF.Abs(x - o.X) < d && MathF.Abs(z - o.Z) < d && MathF.Sqrt((x - o.X) * (x - o.X) + (z - o.Z) * (z - o.Z)) < d && test(name, kind);
                        }))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            return kept.Where(o => !Near(o, 1, (_, _) => true) && !Near(o, 3, (n, _) => n == o.Name) && !Near(o, Farthest, (_, k) => k == ObjectKind.Buildings)).ToList();
        });
    }

    /// <summary>
    /// The farthest something standing can stop a spot from: a building within 4 m.
    /// </summary>
    private const float Farthest = 4;

    /// <summary>
    /// The cell of a coordinate for the neighbour tests: cells a metre wider than <see cref="Farthest"/>, so whatever
    /// is close enough is in the same cell or the next one.
    /// </summary>
    private static int Cell(float v) => (int)MathF.Floor(v / (Farthest + 1));

    /// <summary>The spots as new objects, in one undo step.</summary>
    public static void Commit(EditSession session, string label, IEnumerable<Regrow.Spot> spots) =>
        session.Commit(label, null, [], spots.Select(o => (new NewObject(0, StableHash.Of(o.Name), new Vector3(o.X, o.Y, o.Z), new Vector3(o.Rx, o.Ry, o.Rz), MathF.Abs(o.Scale - 1) < 1e-4f ? 0 : o.Scale), false)).ToList());
}
