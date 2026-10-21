using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opens the editor the keys and mouse tests drive, over a flat area with the objects given.
/// </summary>
internal static class KeysAndMouseSetup
{
    /// <summary>
    /// A shown main window editing a flat 2 × 2 zone area with these objects, looking straight down; with the screen
    /// point of a grid point (on the ground at 30 m).
    /// </summary>
    internal static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open(params WorldScene.Thing[] things)
    {
        var (w, s, screen) = ShownEditor.Open(things);
        w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1);
        return (w, s, (gx, gz) => screen(new Vector3(gx - 64, 30, -(gz - 64))));
    }

    /// <summary>
    /// A half wood wall at grid (40, 40).
    /// </summary>
    internal static WorldScene.Thing Wall() => WallAt(40, 40);

    /// <summary>
    /// A half wood wall at a grid point.
    /// </summary>
    /// <param name="gx">Its grid x.</param>
    /// <param name="gz">Its grid z.</param>
    internal static WorldScene.Thing WallAt(float gx, float gz) => new(11, StableHash.Of("wood_wall_half"), new Vector3(gx - 32, 30, gz - 32), Vector3.Zero, 0, true);

    // ---- [ ] brush size.
}
