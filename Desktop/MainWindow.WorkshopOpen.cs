using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// <see cref="OpenWorkshop"/>'s work, once no other open is under way.
    /// </summary>
    /// <param name="path">The blueprint to open, or null for a blank plot.</param>
    private async Task OpenWorkshopOnce(string? path)
    {
        if (!await WorkshopGameDataReady() || !await MayLeaveForWorkshop())
        {
            return;
        }
        string? name = null, text = null;
        Homestead.Details? details = null;
        if (path != null)
        {
            if (await ReadForWorkshop(path) is not { } read)
            {
                return;
            }
            (name, text, details) = read;
        }
        // Left only once the blueprint could be read.
        if (World is { } open)
        {
            // An area still loading in it is not opened, nor the one left open again.
            _navigation++;
            open.Dispose();
            World = null;
            Session = null;
            Tunnel.Close();
        }
        // The area's name stays English: Claude reads it (editor_state); the title shows it in the user's language.
        var scene = Workshop.Create(name != null ? $"Workshop · {name}" : "Workshop");
        await ShowEditor(scene);
        TitleText.Text = name != null ? Lang.T("workshop.title", name) : Lang.T("workshop.name");
        InWorkshop = true;
        _workshopName = name;
        _workshopLift = 0;
        _workshopTerrain = [];
        _workshopDetails = details;
        // Only Homestead's own folder is saved back to without asking (however its path is written: a separator at the
        // end, a link, Windows' letter case), and then by the path the editor saves under.
        _workshopFile = path != null && Path.GetDirectoryName(Path.GetFullPath(path)) is { } dir
            && Places.FolderKey(dir) == Places.FolderKey(Blueprints.Status.Folder)
            ? Path.Combine(Blueprints.Status.Folder, Path.GetFileName(path)) : null;
        var msg = Said.Of("workshop.msg.welcome");
        if (path != null)
        {
            var (placed, unknown, lift, terrain) = Workshop.Open(scene.Session!, path, text);
            _workshopLift = lift;
            _workshopTerrain = terrain;
            msg = Said.Of("workshop.msg.opened", name, placed) + UnknownLeftOut(unknown) + Said.Of("workshop.msg.changeIt");
        }
        _workshopSaved = scene.Session!.Edits.Version;
        EnterBuilding();
        MapButton.Content = Icons.With("back", Lang.T("workshop.label.start"));
        MapButton.IsVisible = true;
        SupportBox.IsVisible = true;
        DiscardButton.IsVisible = false;
        ShowWorkshopCost();
        Tools.ChooseMode(ToolMode.Place);
        BuildPanel.Card.IsVisible = true;
        RefreshSupport();
        UpdateSaveBar();
        Msg(msg);
    }
}
