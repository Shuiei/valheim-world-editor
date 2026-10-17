using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's test setup: a shown window on a flat area with objects in it, the Select tool's mouse driven at
/// world points, and the objects' boxes. The view's object boxes come from the pieces' own boxes (headless tests draw
/// nothing, so the view is given them).
/// </summary>
internal static class SelectToolRun
{
    internal static readonly int Wall = StableHash.Of("wood_wall_half"), Floor = StableHash.Of("wood_floor"), Beech = StableHash.Of("Beech1");

    /// <summary>
    /// The window, its edit session and the view's size, with the Select tool's mouse driven at world points.
    /// </summary>
    internal sealed record View(MainWindow W, EditSession S, Avalonia.Size Size)
    {
        public SelectTool Tool => W.View.SelectTool;

        /// <summary>
        /// Where a world point is on screen.
        /// </summary>
        public Avalonia.Point At(float x, float y, float z)
        {
            var q = Vector4.Transform(new Vector4(x - S.Scene.Cx, y, -(z - S.Scene.Cz), 1), W.View.ViewProj);
            return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
        }

        /// <summary>
        /// A click at world point (x, z) on the ground, with Shift held and as many clicks as given.
        /// </summary>
        public void Click(float x, float z, bool shift = false, int clicks = 1)
        {
            var p = At(x, 30, z);
            Tool.Down(p, Size, shift, false, clicks);
            Tool.Up(p, Size, shift);
        }

        /// <summary>
        /// A drag along these world points on the ground, with Shift held or not.
        /// </summary>
        public void Drag(bool shift, params (float X, float Z)[] path)
        {
            Tool.Down(At(path[0].X, 30, path[0].Z), Size, shift, false, 1);
            foreach (var (x, z) in path.Skip(1))
            {
                Tool.Moved(At(x, 30, z), Size);
            }
            Tool.Up(At(path[^1].X, 30, path[^1].Z), Size, shift);
        }

        public HashSet<int> Selected => [.. W.View.Selected];

        public WorldScene.Thing Last => S.Scene.Things[^1];
    }

    /// <summary>
    /// A box around a thing in view space: a piece's own box, or 1 × 5 × 1 m for a tree.
    /// </summary>
    internal static (int, Vector3, Vector3) Box(WorldScene s, int i)
    {
        var t = s.Things[i];
        var info = PieceCatalog.Get(t.Prefab);
        var (x0, x1, z0, z1, y0, y1) = info != null ? (info.MinX, info.MaxX, info.MinZ, info.MaxZ, info.MinY, info.MaxY) : (-0.5f, 0.5f, -0.5f, 0.5f, 0f, 5f);
        var p = t.Position;
        return (i, new Vector3(p.X + x0 - s.Cx, p.Y + y0, -(p.Z + z1 - s.Cz)), new Vector3(p.X + x1 - s.Cx, p.Y + y1, -(p.Z + z0 - s.Cz)));
    }

    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects (given their boxes) in the Select tool,
    /// looking straight down on the middle.
    /// </summary>
    internal static View Open(params WorldScene.Thing[] things)
    {
        var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
        w.Show();
        var s = TestScenes.Flat(2, things);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        w.View.SetBoxes(Enumerable.Range(0, things.Length).Select(i => Box(s.Scene, i)));
        // Straight down on the middle of the area (a little south, so north is up), 40 m around.
        var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
        w.View.SetCamera(c + new Vector3(0, 40, 0.01f), c, 1);
        return new View(w, s, w.View.Bounds.Size);
    }

    /// <summary>
    /// A thing of this kind at world point (x, y, z): a building piece, or a tree when not.
    /// </summary>
    internal static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, piece ? 0 : 1, piece);
}
