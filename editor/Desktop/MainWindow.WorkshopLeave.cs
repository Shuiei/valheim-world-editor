using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The sentence naming the kinds the game does not know that a blueprint put on the plot left out (the first five),
    /// empty when there are none.
    /// </summary>
    /// <param name="unknown">The kinds left out (game data, the same in every language).</param>
    private static Said UnknownLeftOut(List<string> unknown) =>
        unknown.Count > 0 ? Said.Of("blueprints.msg.unknownImported", unknown.Count, string.Join(", ", unknown.Take(5))) : Said.Empty;

    /// <summary>
    /// Leaving an area: no Workshop any more (ShowEditor calls it for every area).
    /// </summary>
    private void ResetWorkshop()
    {
        LeaveBuilding();
        InWorkshop = false;
        ToolTip.SetTip(SubtitleText, null);
        MapButton.Content = Icons.With("back", Lang.T("top.label.map"));
        SupportBox.IsVisible = false;
        DiscardButton.IsVisible = true;
        LastSupport = null;
        View.ShowSupport(null);
    }

    /// <summary>
    /// Start: back to the start page, asking first when the building is not saved as a blueprint.
    /// </summary>
    internal async Task LeaveWorkshop()
    {
        if (WorkshopDirty && !await Ask(Lang.T("workshop.ask.leave.title"), Lang.T("workshop.ask.leave.text"), Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        ResetWorkshop();
        Session = null;
        ShowStart();
    }

    /// <summary>
    /// Save blueprint: the building pieces on the plot, as a Homestead blueprint (asks first when the support check
    /// says some would fall in game).
    /// </summary>
    internal async Task SaveWorkshop()
    {
        if (Session is not { } s)
        {
            return;
        }
        View.SelectTool.Commit();
        if (Workshop.Pieces(s.Scene) == 0)
        {
            await Tell(Lang.T("workshop.noPiecesToSave"));
            return;
        }
        RefreshSupport();
        // Worked out even when the Support check is off: its switch only shows the colours.
        if (SolveSupport(s).Result is { Breaking: > 0 } r
            && !await Ask(Lang.T("workshop.label.saveBlueprint"), Lang.T("workshop.ask.falling.text", r.Breaking), Lang.T("workshop.ask.falling.yes"), Lang.T("common.keepBuilding")))
        {
            return;
        }
        string name = _workshopName ?? Lang.T("workshop.defaultName");
        var clip = Workshop.Building(s.Scene, name, _workshopLift);
        if (await Blueprints.SaveBuilding(clip, WorkshopDetails ?? new Homestead.Details(name, "", []), _workshopFile) is { } details)
        {
            WorkshopSavedAs(s, details);
        }
    }

    /// <summary>The plot's building is saved under these details: the Workshop takes its name and is no longer dirty.</summary>
    private void WorkshopSavedAs(EditSession s, Homestead.Details details)
    {
        _workshopName = details.Name;
        WorkshopDetails = details;
        _workshopFile = Path.Combine(Blueprints.Status.Folder, Homestead.FileName(details.Name));
        BuildPanel.RenderLibrary();
        _workshopSaved = s.Edits.Version;
        TitleText.Text = Lang.T("workshop.title", details.Name);
        UpdateSaveBar();
    }
}
