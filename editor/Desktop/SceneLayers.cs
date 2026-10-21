using System.Numerics;
using System.Runtime.CompilerServices;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// What mods' extensions draw of an open area as a whole (ModExtensions.ScenesOf): their meshes, the ground cells
/// their holes leave undrawn, and the meshes' triangles for picking, worked out on a worker and kept per scene with the
/// state it was worked out at (the edits, the live game, the objects, the extensions' generation). The view shows what
/// was worked out last while the next is worked out.
/// </summary>
public static partial class SceneLayers
{
    /// <summary>What was worked out for a scene.</summary>
    /// <param name="Layers">The extensions' layers, as they gave them (checked).</param>
    /// <param name="Holes">One byte a ground cell (W - 1 by H - 1, row by row from the south): 1 where it is not drawn; empty for none.</param>
    /// <param name="Picks">The pickable meshes' triangles, in the view's frame.</param>
    public sealed record Built(IReadOnlyList<SceneLayer> Layers, byte[] Holes, PickTriangles Picks)
    {
        /// <summary>Nothing drawn.</summary>
        public static Built None { get; } = new([], [], PickTriangles.None);
    }

    /// <summary>What was worked out last, per scene, with the state it was for.</summary>
    private sealed record Cache(object At, Built Value);

    private static readonly ConditionalWeakTable<WorldScene, Cache> Caches = [];
    private static readonly ConditionalWeakTable<WorldScene, object> Working = [];

    /// <summary>The state a scene's layers depend on: its objects and edits, its live game, the extensions.</summary>
    private static object Now(WorldScene s) => (SceneReads<object>.Now(s), ModExtensions.Generation);

    /// <summary>What was worked out last for a scene (nothing yet: <see cref="Built.None"/>), without working.</summary>
    public static Built Last(WorldScene s) => Caches.TryGetValue(s, out var c) ? c.Value : Built.None;

    /// <summary>
    /// For drawing (each frame): what was worked out last; when the scene changed, it is worked out again on a worker,
    /// and ready is called when that is done (or failed: logged, the last kept).
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="ready">Called when a new result is in.</param>
    public static Built Shown(WorldScene s, Action ready)
    {
        if (!ModExtensions.AnyScenes)
        {
            return Last(s).Layers.Count == 0 ? Last(s) : Keep(s, Now(s), Built.None);
        }
        object now = Now(s);
        Caches.TryGetValue(s, out var c);
        if (c is { } k && k.At.Equals(now))
        {
            return k.Value;
        }
        if (Working.TryAdd(s, new object()))
        {
            // The area as it is now, taken here: the work on the worker reads this, not the scene as it goes on changing.
            var area = new AreaView(s);
            Task.Run(() =>
            {
                try
                {
                    Keep(s, now, Build(s, area));
                    ready();
                }
                catch (Exception e)
                {
                    // An extension's layers that cannot be drawn: the last ones stay, not worked out again until the
                    // scene changes.
                    Log.Caught(e, "working out mods' extensions' layers for the 3D view (the last ones stay)");
                    Keep(s, now, c?.Value ?? Built.None);
                }
                finally
                {
                    Working.Remove(s);
                }
            });
        }
        return c?.Value ?? Built.None;
    }

    private static Built Keep(WorldScene s, object at, Built value)
    {
        Caches.AddOrUpdate(s, new Cache(at, value));
        return value;
    }

    /// <summary>
    /// The extensions' layers of a scene worked out now, on the caller's thread, and kept as what was worked out last
    /// (what picking and the view use).
    /// </summary>
    /// <param name="s">The scene.</param>
    internal static Built Refresh(WorldScene s) => Keep(s, Now(s), Build(s, new AreaView(s)));

    /// <summary>The extensions' layers of a scene's area, with its hole cells and pick triangles (on the caller's thread).</summary>
    /// <param name="s">The scene.</param>
    /// <param name="area">Its area, as taken for the extensions.</param>
    internal static Built Build(WorldScene s, AreaView area)
    {
        var layers = ModExtensions.ScenesOf(area);
        return new Built(layers, HoleCells(s, layers), PickTriangles.Of(s, layers));
    }

    /// <summary>
    /// The ground cells the layers' holes leave undrawn: a cell (a metre square between four grid points) is undrawn
    /// when it lies inside a hole; empty when no hole touches the area.
    /// </summary>
    internal static byte[] HoleCells(WorldScene s, IReadOnlyList<SceneLayer> layers)
    {
        int cw = s.W - 1, ch = s.H - 1;
        int ox = (int)s.OriginX, oz = (int)s.OriginZ;
        byte[]? cells = null;
        foreach (var h in layers.SelectMany(l => l.Holes))
        {
            int x0 = Math.Max(0, h.X0 - ox), x1 = Math.Min(cw, h.X1 - ox), z0 = Math.Max(0, h.Z0 - oz), z1 = Math.Min(ch, h.Z1 - oz);
            if (x0 >= x1 || z0 >= z1)
            {
                // Outside the area.
                continue;
            }
            cells ??= new byte[cw * ch];
            for (int z = z0; z < z1; z++)
            {
                Array.Fill(cells, (byte)1, z * cw + x0, x1 - x0);
            }
        }
        return cells ?? [];
    }

    /// <summary>Whether the ground cell under a view-space point is left undrawn by a layer's hole.</summary>
    /// <param name="s">The scene.</param>
    /// <param name="holes">Its hole cells (<see cref="Built.Holes"/>).</param>
    /// <param name="x">View x.</param>
    /// <param name="z">View z.</param>
    internal static bool HoleAt(WorldScene s, byte[] holes, float x, float z)
    {
        if (holes.Length == 0)
        {
            return false;
        }
        int gx = (int)MathF.Floor(x + (s.W - 1) / 2f), gz = (int)MathF.Floor(-z + (s.H - 1) / 2f);
        return gx >= 0 && gz >= 0 && gx < s.W - 1 && gz < s.H - 1 && holes[gz * (s.W - 1) + gx] != 0;
    }

    /// <summary>A world point in the view's frame (x - Cx, y, -(z - Cz)).</summary>
    internal static Vector3 ToView(WorldScene s, Vector3 p) => new(p.X - s.Cx, p.Y, -(p.Z - s.Cz));
}
