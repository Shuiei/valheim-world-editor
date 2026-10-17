namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A flat one-zone test scene with one shape step made on it at the middle of the scene.
/// </summary>
internal static class ShapedChange
{
    /// <summary>
    /// The scene and the change of a shape step at grid point (32, 32) with a 3 m brush, its height given by <paramref name="f"/>.
    /// </summary>
    internal static (EditSession Session, EditSession.Change Change) Make(Func<Formula.Env, double> f)
    {
        var s = TestScenes.Flat(1);
        s.Shape(32, 32, f, 3, 0, "A");
        return (s, s.UndoList[^1]);
    }
}
