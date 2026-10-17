using TerrainEditor.Editing;

namespace WorldEditor.Tests;

/// <summary>
/// A dungeon planner made for a test, with its private steps called one at a time (the way <c>Build</c> calls them).
/// </summary>
internal sealed class DungeonPlannerProbe
{
    private readonly object _planner;

    /// <summary>Makes a planner for these settings and style, drawing from <paramref name="rnd"/>.</summary>
    public DungeonPlannerProbe(DungeonGen.Settings s, object style, Random rnd)
    {
        _planner = PrivateCall.Create(typeof(DungeonPlanner), [typeof(DungeonGen.Settings), style.GetType(), typeof(Random)], s, style, rnd);
    }

    /// <summary>The plan being made.</summary>
    public DungeonPlan Plan => (DungeonPlan)PrivateCall.Field(_planner, "_plan")!;

    /// <summary>Calls a private instance step of the planner.</summary>
    public object? Call(string name, params object?[] args) => PrivateCall.Instance(_planner, name, args);

    /// <summary>Adds an empty level to the plan, at the next index.</summary>
    public DungeonPlan.Level AddLevel()
    {
        var level = new DungeonPlan.Level { Index = Plan.Levels.Count };
        Plan.Levels.Add(level);
        return level;
    }

    /// <summary>Makes a space of a kind of room on a level, as the planner does.</summary>
    public DungeonPlan.Space AddRoom(DungeonPlan.Level level, DungeonKit.Room room, (int X, int Z) dir, (int X0, int Z0, int W, int H) box) =>
        (DungeonPlan.Space)Call("NewSpace", level, room, dir, box, null)!;

    /// <summary>Gives a space the tiles of a box of this many tiles from the origin, as a room has (for tests of its walls).</summary>
    public static void FillBox(DungeonPlan.Space space, int width, int depth)
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                space.Tiles.Add((i, j));
            }
        }
    }

    /// <summary>Lays the entrance on a level, as the planner's first step does.</summary>
    public DungeonPlan.Space PlaceEntrance(DungeonPlan.Level level) => (DungeonPlan.Space)Call("PlaceEntrance", level)!;

    /// <summary>A planner for a Crypt dungeon of these settings, with its first level added.</summary>
    public static (DungeonPlannerProbe P, DungeonPlan.Level L0) Start(DungeonGen.Settings s)
    {
        var p = new DungeonPlannerProbe(s, DungeonKit.StyleOf("Crypt"), new Random(s.Seed));
        return (p, p.AddLevel());
    }

    /// <summary>A planner of one level, with its entrance placed.</summary>
    public static (DungeonPlannerProbe P, DungeonPlan.Level L0, DungeonPlan.Space Entrance) Entered(int seed = 1)
    {
        var (p, l0) = Start(new DungeonGen.Settings(Size: 3, Levels: 1, Seed: seed));
        return (p, l0, p.PlaceEntrance(l0));
    }

    /// <summary>A placement of a room in a box, made the planner's way (its record type is private to the planner).</summary>
    public static object PlacementOf((int X0, int Z0, int W, int H) box, int shift, int length, bool tall, List<(int, int)> corridor, (int, int) t1, (int, int) t2)
    {
        var type = typeof(DungeonPlanner).GetNestedType("Placement", System.Reflection.BindingFlags.NonPublic)!;
        return PrivateCall.Create(type,
            [typeof((int X0, int Z0, int W, int H)), typeof(List<(int, int)>), typeof((int, int)), typeof((int, int)), typeof((int X, int Z)), typeof(int),
                typeof(int), typeof(bool), typeof(DungeonPlan.Shape), typeof(List<(int, int)>), typeof((int, int)), typeof((int, int)), typeof((int X, int Z))],
            box, corridor, t1, t2, (0, 1), shift, length, tall, DungeonPlan.Shape.Rect, new List<(int, int)>(), t1, t2, (0, 1));
    }

    /// <summary>Reads a property of a placement.</summary>
    public static object? Property(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o);
}
