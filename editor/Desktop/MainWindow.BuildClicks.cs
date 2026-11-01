using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>Building's own clicks from the 3D view: Shift + right click takes a piece away, a middle click builds with its kind.</summary>
    private void WireBuildClicks()
    {
        View.RemoveAsked += RemoveClicked;
        View.KindAsked += KindClicked;
    }

    /// <summary>Shift + right click while building: the object (and its linked parts) taken away, as one undo step.</summary>
    /// <param name="index">The object, an index into the scene's things.</param>
    internal void RemoveClicked(int index)
    {
        if (Session is not { } s || index < 0 || index >= s.Scene.Things.Count || s.Scene.Things[index].Gone)
        {
            return;
        }
        string name = PieceCost.PieceName(PrefabCatalog.NameOf(s.Scene.Things[index].Prefab) ?? "?");
        s.Delete([index]);
        PlaceInput.Refresh();
        Msg(Said.Of("build.msg.removed", name));
    }

    /// <summary>
    /// Middle click while building: its kind becomes the piece built (and first among those picked last), when the Build
    /// dock lists it; else the status bar says it is not one to build.
    /// </summary>
    /// <param name="index">The object, an index into the scene's things.</param>
    internal void KindClicked(int index)
    {
        if (Session is not { } s || index < 0 || index >= s.Scene.Things.Count)
        {
            return;
        }
        string? name = PrefabCatalog.NameOf(s.Scene.Things[index].Prefab);
        if (name != null && BuildPanel.Pieces.Any(p => p.Prefab == name))
        {
            BuildPanel.Pick(name);
            Msg(Said.Of("build.msg.picked", PieceCost.PieceName(name)));
        }
        else
        {
            Msg(Said.Of("build.msg.notAPiece", name ?? "?"));
        }
    }
}
