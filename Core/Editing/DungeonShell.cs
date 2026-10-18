using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// What a generated dungeon is made of, as it is laid out: objects with their place and turn, and the
/// object data some need (a chest's contents, a sign's text, a creature's stars). The same object twice
/// at the same place (a slab shared by two levels, a wall seen from both sides) is kept once.
/// </summary>
internal sealed class DungeonOut
{
    /// <summary>The objects so far, in the order added.</summary>
    public readonly List<DungeonGen.Item> Items = [];
    /// <summary>Prefab and position (5 cm steps) of every object added, to keep each once.</summary>
    private readonly HashSet<(string, int, int, int)> _seen = [];

    /// <summary>A turn about the vertical axis, in degrees.</summary>
    public static Quaternion Yaw(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);

    /// <summary>The turn (degrees) that faces a grid way: 0 for +z, 90 for +x.</summary>
    public static float YawOf((int X, int Z) d) => MathF.Atan2(d.X, d.Z) * 180f / MathF.PI;

    /// <summary>Adds an object turned <paramref name="yaw"/> degrees, unless the same prefab is already at that place.</summary>
    public void Add(string prefab, Vector3 p, float yaw, IReadOnlyList<(string Section, string Key, string Value)>? data = null) => Add(prefab, p, Yaw(yaw), data);

    /// <summary>
    /// Adds an object, unless the same prefab is already at that place (to 5 cm); data: values of its object
    /// data (section, key, value as text).
    /// </summary>
    public void Add(string prefab, Vector3 p, Quaternion q, IReadOnlyList<(string Section, string Key, string Value)>? data = null)
    {
        if (_seen.Add((prefab, (int)MathF.Round(p.X * 20), (int)MathF.Round(p.Y * 20), (int)MathF.Round(p.Z * 20))))
        {
            Items.Add(new DungeonGen.Item(prefab, p, q, data));
        }
    }
}

/// <summary>The building: floor and roof slabs, walls, doorways, posts, beams, floor finish, stairs.</summary>
internal static partial class DungeonShell
{
    /// <summary>The four ways along the grid: +z, +x, -z, -x.</summary>
    private static readonly (int X, int Z)[] Dirs = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    /// <summary>
    /// Builds every level of the plan in the material: slabs, walls, doorways, the floor finish (when the
    /// settings' Decor is 1 or more), beams and stairs.
    /// </summary>
    public static void Build(DungeonPlan plan, Material mat, DungeonOut o, DungeonGen.Settings s)
    {
        foreach (var l in plan.Levels)
        {
            float f = DungeonPlan.FloorY(l.Index);
            Slabs(l, f, o);
            Walls(plan, l, f, mat, o);
            foreach (var d in plan.Doors.Where(d => d.A.Level == l.Index))
            {
                Doorway(d, f, mat, o, BiomeOf(s.Biome).Name);
            }
            if (s.Decor >= 1)
            {
                // The floor finish: a room's own, else the material's.
                foreach (var (t, sp) in l.Owner)
                {
                    string? finish = sp.Floor ?? mat.Floor;
                    if (finish == null || sp.Room is Room.StairTop or Room.StairBottom || l.Holes.Contains(DungeonPlan.CellOf(t)))
                    {
                        continue;
                    }
                    o.Add(finish, new Vector3(2 * t.Item1 + 1, f, 2 * t.Item2 + 1), 0);
                }
            }
            if (mat.Beam != null)
            {
                foreach (var sp in l.Spaces.Where(x => x.IsRoom && x.Room is not (Room.StairTop or Room.StairBottom)))
                {
                    Beams(sp, f, mat, o);
                }
            }
            foreach (var top in l.Spaces.Where(x => x.Room == Room.StairTop))
            {
                Stairs(top, f, o);
            }
        }
    }

    /// <summary>
    /// Black marble slabs (8 × 8 × 2 m), the floor and roof of every cell in use: the one piece the game
    /// never asks for support, so all else can stand on them, high in the sky.
    /// </summary>
    private static void Slabs(DungeonPlan.Level l, float f, DungeonOut o)
    {
        var floors = new HashSet<(int, int)>();
        var roofs = new HashSet<(int, int)>();
        var high = new HashSet<(int, int)>();
        foreach (var (t, sp) in l.Owner)
        {
            var c = DungeonPlan.CellOf(t);
            if (!l.Holes.Contains(c))
            {
                floors.Add(c);
            }
            if (l.Open.Contains(c))
            {
                continue;
            }
            (sp.Tall ? high : roofs).Add(c);
        }
        foreach (var (ci, cj) in floors)
        {
            o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, f - 1, 8 * cj + 4), 0);
        }
        foreach (var (ci, cj) in roofs)
        {
            o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, f + 5, 8 * cj + 4), 0);
        }
        foreach (var (ci, cj) in high)
        {
            o.Add("blackmarble_floor_large", new Vector3(8 * ci + 4, f + 11, 8 * cj + 4), 0);
        }
    }
}
