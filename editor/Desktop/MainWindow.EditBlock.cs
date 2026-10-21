using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// A block of zones of the world in the 3D editor (it need not be square: Follow grows it); an object the map's
    /// search found at <paramref name="spot"/> is selected, the camera on it.
    /// </summary>
    /// <param name="x0">The south-west zone, east.</param>
    /// <param name="z0">The south-west zone, north.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="spot">The map's chosen zone the area was opened around, if it was.</param>
    /// <param name="context">Said before the failure, when it cannot be opened.</param>
    /// <returns>
    /// Whether the block was opened (no: the failure is said in the status bar; when this was the last area asked for
    /// and the 3D editor is left with no area open, the map is shown and the failure in a dialog).
    /// </returns>
    internal async Task<bool> EditBlock(int x0, int z0, int zonesX, int zonesZ, (int X, int Z)? spot = null, Said? context = null)
    {
        if (World is not { } world)
        {
            return false;
        }
        long navigation = ++_navigation;
        View.SelectTool.Commit();
        var left = KeepHistory();
        Busy(Lang.T("world.msg.loadingZones", zonesX, zonesZ));
        Said failed;
        try
        {
            await BeforeLoad();
            var scene = await Task.Run(() => WorldScene.LoadBlock(world, x0, z0, zonesX, zonesZ));
            if (!await ShowEditor(scene, () => navigation == _navigation))
            {
                // The map or another area was asked for while it loaded: it is not shown, nor the world's area.
                if (world.Area == scene.Session)
                {
                    world.Area = Session;
                }
                Report(Said.Of("world.msg.areaDropped"));
                return false;
            }
            // Shown: the world's area, even if a load dropped meanwhile had reset it.
            world.Area = scene.Session;
            FocusSearchTarget(scene, spot);
            return true;
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "opening an area");
            Unleave(left, navigation);
            failed = (context ?? Said.Empty) + Said.Of("world.msg.openAreaFailed", ex.Message);
            Report(failed);
        }
        finally
        {
            Busy(null);
        }
        // The last area asked for failed with none open behind the 3D editor (closed for this one, or for another that
        // failed too): the map, not tools that silently do nothing, and why in a dialog (the map has no status bar).
        if (navigation == _navigation && Session == null && _pages.Content == _editorPage)
        {
            ShowMap();
            await Tell(failed.Shown);
        }
        return false;

        // The world left meanwhile: only the status bar, no dialog about a world no longer open.
        void Report(Said text)
        {
            if (World == world)
            {
                Say(text);
            }
            else
            {
                Msg(text);
            }
        }
    }
}
