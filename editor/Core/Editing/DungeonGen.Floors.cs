using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

public static partial class DungeonGen
{
    /// <summary>
    /// A floor of its own for some rooms: in a stone or grausten dungeon, the grand rooms (halls, throne room, chapels,
    /// the arena, the treasury, the entrance) get black marble tiles half the time, and quarters, barracks, dens and
    /// studies wooden boards (with Decor 1 or more; the shell lays them).
    /// </summary>
    private static void Floors(DungeonPlan plan, Material mat, Settings s, Random rnd)
    {
        if (s.Decor < 1 || mat.Name is not ("Stone" or "Grausten"))
        {
            return;
        }
        foreach (var sp in plan.Spaces.Where(sp => sp.IsRoom))
        {
            bool grand = sp.Room is Room.Hall or Room.Throne or Room.Chapel or Room.Boss or Room.Treasury or Room.Entrance;
            bool lived = sp.Room is Room.Quarters or Room.Barracks or Room.Den or Room.Library;
            if (grand && rnd.NextDouble() < 0.5)
            {
                (sp.Floor, sp.FloorTop) = ("blackmarble_tile_floor_2x2", 0.19f);
            }
            else if (lived && rnd.NextDouble() < 0.5)
            {
                (sp.Floor, sp.FloorTop) = ("wood_floor", 0.1f);
            }
        }
    }

    /// <summary>
    /// A shaped room's floor as rectangles (metres): each row of its tiles in runs, rows with the same runs
    /// merged into one rectangle (an L is two, a cross three).
    /// </summary>
    private static List<(float X0, float Z0, float X1, float Z1)> Runs(DungeonPlan.Space sp)
    {
        var parts = new List<(float X0, float Z0, float X1, float Z1)>();
        foreach (var row in sp.Tiles.GroupBy(t => t.J).OrderBy(g => g.Key))
        {
            var xs = row.Select(t => t.I).Order().ToList();
            for (int a = 0; a < xs.Count;)
            {
                int b = a;
                while (b + 1 < xs.Count && xs[b + 1] == xs[b] + 1)
                {
                    b++;
                }
                float x0 = 2 * xs[a], x1 = 2 * xs[b] + 2, z0 = 2 * row.Key;
                int same = parts.FindIndex(p => p.X0 == x0 && p.X1 == x1 && p.Z1 == z0);
                if (same >= 0)
                {
                    parts[same] = (x0, parts[same].Z0, x1, z0 + 2);
                }
                else
                {
                    parts.Add((x0, z0, x1, z0 + 2));
                }
                a = b + 1;
            }
        }
        return parts;
    }
}
