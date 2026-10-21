namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Leaving an area: its history stays with the world, for the next area opened, and the area is closed, so work
    /// awaited in it (a script, Grow) finds it gone and changes nothing.
    /// </summary>
    /// <returns>The area left, or null when none was open in the 3D editor.</returns>
    private EditSession? KeepHistory()
    {
        if (Session is { Scene.Owner: { } owner } s && _pages.Content == _editorPage)
        {
            owner.History = s.Export();
            owner.Area = null;
            Session = null;
            return s;
        }
        return null;
    }

    /// <summary>
    /// Counts the map shown, the areas asked for and the world left: an area load that finished after one of those is
    /// not shown, and one that failed leaves the area it left closed.
    /// </summary>
    private long _navigation;

    /// <summary>
    /// Another area could not be opened: the one left (still shown) is open again, its history its own, unless the map
    /// or another area was asked for meanwhile.
    /// </summary>
    /// <param name="left">What <see cref="KeepHistory"/> returned.</param>
    /// <param name="navigation">The <see cref="_navigation"/> count of the failed load.</param>
    private void Unleave(EditSession? left, long navigation)
    {
        if (left is { Scene.Owner: { } owner } && navigation == _navigation && World == owner && _pages.Content == _editorPage
            && Session == null && View.Scene == left.Scene)
        {
            owner.Area = left;
            Session = left;
        }
    }

    /// <summary>
    /// An area of the world in the 3D editor; an object the map's search found there is selected, the camera on it.
    /// </summary>
    /// <param name="x">The middle zone, east.</param>
    /// <param name="z">The middle zone, north.</param>
    /// <param name="size">The area's side, in zones.</param>
    internal Task EditArea(int x, int z, int size) => EditBlock(x - size / 2, z - size / 2, size, size, (x, z));
}
