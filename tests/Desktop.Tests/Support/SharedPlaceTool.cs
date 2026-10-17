
namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A Place tool as the placement tests set it up: on a flat area, with the kinds chosen the way the panel chooses them.
/// </summary>
internal static class SharedPlaceTool
{
    /// <summary>
    /// A Place tool on a flat 2 × 2 zone area with these objects, Beech1 chosen and no tilt.
    /// </summary>
    internal static (PlaceTool T, EditSession S) Tool(params WorldScene.Thing[] things)
    {
        var s = TestScenes.Flat(2, things);
        var t = new PlaceTool { Scene = () => s.Scene, Brush = s.Brush };
        t.Chosen.Clear();
        t.Chosen.Add("Beech1");
        t.Tilt = 0;
        return (t, s);
    }

    /// <summary>
    /// Chooses this kind alone, snapped, and sets the Line mode with the shape given.
    /// </summary>
    internal static void ChooseWallLine(PlaceTool t, string kind, PlaceTool.LineShapes shape)
    {
        t.Chosen.Clear();
        t.Chosen.Add(kind);
        t.AutoSnap();
        t.Mode = PlaceTool.Modes.Line;
        t.LineShape = shape;
    }
}
