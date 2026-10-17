using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// Lays out a <see cref="DungeonPlan"/> from the settings and a style: on each level the way through, stairs
/// down or the antechamber, arena and treasury, rooms off the way (some twinned), then loops; then the
/// boss's key and hidden caches, and what stands in each doorway.
/// </summary>
internal sealed partial class DungeonPlanner
{
    /// <summary>The four ways along the grid: +z, +x, -z, -x.</summary>
    private static readonly (int X, int Z)[] Dirs = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    /// <summary>The plan being made.</summary>
    private readonly DungeonPlan _plan = new();
    private readonly DungeonGen.Settings _s;
    private readonly Style _style;
    private readonly Random _rnd;
    /// <summary>Tiles from the origin a level may reach.</summary>
    private readonly int _extent;
    /// <summary>The kinds of room made so far, so unique ones come once.</summary>
    private readonly HashSet<Room> _used = [];
    /// <summary>The next space's <c>Id</c>.</summary>
    private int _next;

    /// <summary>Sets up a planner; the size of the settings decides how far a level may reach.</summary>
    private DungeonPlanner(DungeonGen.Settings s, Style style, Random rnd)
    {
        _s = s;
        _style = style;
        _rnd = rnd;
        _extent = 14 + 6 * Math.Clamp(s.Size, 1, 4);
    }

    /// <summary>Lays out a dungeon; everything random comes from <paramref name="rnd"/>.</summary>
    public static DungeonPlan Make(DungeonGen.Settings s, Style style, Random rnd)
    {
        var p = new DungeonPlanner(s, style, rnd);
        p.Build();
        return p._plan;
    }

    /// <summary>The way to the right of one facing <paramref name="d"/>.</summary>
    private static (int X, int Z) Right((int X, int Z) d) => (d.Z, -d.X);

    /// <summary>The tile <paramref name="n"/> steps from <paramref name="t"/> along <paramref name="d"/>.</summary>
    private static (int, int) Add((int I, int J) t, (int X, int Z) d, int n = 1) => (t.I + d.X * n, t.J + d.Z * n);

    /// <summary>A random number from min to max in steps of 2 (min, min + 2, ...).</summary>
    private int Even(int min, int max) => min + 2 * _rnd.Next((max - min) / 2 + 1);

    /// <summary>One of the items at random, each as likely as its weight.</summary>
#pragma warning disable S2245 // A plan must come out the same from its seed; nothing here is a secret.
    private T Pick<T>(IReadOnlyList<(T Item, float Weight)> items) => Weighted.Pick(items, i => i.Weight, _rnd.NextDouble()).Item;
#pragma warning restore S2245
}
