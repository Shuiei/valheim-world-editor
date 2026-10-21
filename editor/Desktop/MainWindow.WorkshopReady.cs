using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Reads the game's files the Workshop needs (its pieces, and how ground discs shape the plot) and the player's
    /// mods' data names and manifests (their pieces' groups, the ones not offered). False when the game is missing,
    /// said on the start page when nothing is open, else in the status bar.
    /// </summary>
    private async Task<bool> WorkshopGameDataReady()
    {
        bool ready = await Task.Run(() =>
        {
            if (!GameData.Ensure(GameLook.Bundles))
            {
                return false;
            }
            WorldSession.ReadModFiles();
            return true;
        });
        if (!ready)
        {
            if (World == null && !InWorkshop)
            {
                ShowStart(GameData.Missing);
            }
            else
            {
                MessageText.Text = GameData.Missing;
            }
        }
        return ready;
    }

    /// <summary>
    /// Asks before the Workshop replaces what is open: a world with changes not saved (or not applied), or a building
    /// not saved. False when the user stays.
    /// </summary>
    private async Task<bool> MayLeaveForWorkshop()
    {
        if (World is { } w && w.Pending is not (0, 0, 0, 0))
        {
            string what = Describe(w.Pending);
            string text = w.IsLive ? Lang.T("workshop.ask.leaveWorld.textLive", what) : Lang.T("workshop.ask.leaveWorld.text", what);
            if (!await Ask(Lang.T("save.ask.leave.title"), text, Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
            {
                return false;
            }
        }
        return !WorkshopDirty || await Ask(Lang.T("workshop.name"), Lang.T("workshop.ask.openAnother.text"), Lang.T("workshop.ask.openAnother.yes"), Lang.T("common.stay"));
    }

    /// <summary>
    /// Reads a blueprint to open in the Workshop: its name, its text and its details. Null when it cannot be read,
    /// with the reason in the status bar (the world stays open as it was).
    /// </summary>
    /// <param name="path">The blueprint's file.</param>
    private async Task<(string Name, string Text, Homestead.Details Details)?> ReadForWorkshop(string path)
    {
        try
        {
            string text = await File.ReadAllTextAsync(path);
            var parsed = BlueprintFormats.Parse(path, text);
            string name = parsed.Name;
            var details = Homestead.Read(path)?.Details is { } d ? d with { Name = name } : new Homestead.Details(name, parsed.Description ?? "", []);
            return (name, text, details);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading a blueprint to open it in the Workshop");
            Msg("workshop.msg.openFailed", ex.Message);
            return null;
        }
    }
}
