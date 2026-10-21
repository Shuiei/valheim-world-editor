using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// What was dropped at a point of the view.
    /// </summary>
    /// <param name="blueprint">A Library blueprint's path, or null.</param>
    /// <param name="files">Files from the computer; only .blueprint and .vbuild ones are taken.</param>
    /// <param name="at">Where it was dropped, on the view.</param>
    /// <param name="size">The view's size, to find the ground under that point.</param>
    internal void Drop(string? blueprint, IReadOnlyList<string> files, Avalonia.Point at, Avalonia.Size size)
    {
        Vector2? world = null;
        if (Session is { } s && View.GridAt(at, size) is { } g)
        {
            world = new Vector2(s.Scene.OriginX + g.X, s.Scene.OriginZ + g.Y);
        }
        var paths = new List<string>();
        if (blueprint != null)
        {
            paths.Add(blueprint);
        }
        foreach (string f in files.Where(f => f.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase)))
        {
            if (Blueprints.Import(f) is string name)
            {
                paths.Add(Path.Combine(Blueprints.Status.Folder, Homestead.FileName(name)));
            }
        }
        if (files.Count > 0 && paths.Count == (blueprint != null ? 1 : 0))
        {
            Msg("workshop.msg.dropFiles");
            return;
        }
        BuildPanel.RenderLibrary();
        if (!InWorkshop)
        {
            return;
        }
        foreach (string p in paths)
        {
            AddToWorkshop(p, world);
        }
    }
}
