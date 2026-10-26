using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool tests' setup: a flat area with a tree and a wall in it, shown in a main window in the Select tool,
/// and the last thing added to a scene.
/// </summary>
internal static class SelectAreaRun
{
    internal static readonly int Beech = StableHash.Of("Beech1");
    internal static readonly int Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// A flat 2 × 2 zone area (world x and z from -32 to 96) with a tree and a wall.
    /// </summary>
    internal static EditSession Area() => TestScenes.Flat(2,
        new WorldScene.Thing(10, Beech, new Vector3(20, 30, 20), Vector3.Zero, 1, false),
        new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), new Vector3(0, 90, 0), 0, true));

    /// <summary>
    /// A shown main window editing the mound area, in the Select tool.
    /// </summary>
    internal static (MainWindow W, EditSession S) Window()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        var s = Area();
        w.View.Show(s.Scene, null);
        w.Edit(s);
        w.Tools.ChooseSelect();
        return (w, s);
    }

    /// <summary>
    /// The last thing in the scene (the one just added).
    /// </summary>
    internal static WorldScene.Thing Last(EditSession s) => s.Scene.Things[^1];
}
