using Avalonia.Controls;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// Shows the building's cost under the title, the full list of materials and stations in its tip.
    /// </summary>
    private void ShowWorkshopCost()
    {
        if (!InWorkshop || Session is not { } s)
        {
            return;
        }
        var cost = PieceCost.Of(Workshop.Kinds(s.Scene));
        WorkshopCost = cost.Describe(5);
        SubtitleText.Text = Workshop.Pieces(s.Scene) == 0 ? Lang.T("workshop.blankPlot") : Lang.T("blueprints.cost", WorkshopCost);
        ToolTip.SetTip(SubtitleText, cost.Materials.Count == 0 ? null
            : string.Join("\n", cost.Materials.Select(m => $"{m.Name}: {m.Amount}")) + (cost.Stations.Count > 0 ? "\n" + Lang.T("workshop.near", string.Join(", ", cost.Stations)) : "")
                + (cost.Unknown.Count > 0 ? "\n" + Lang.T("workshop.noRecipe", string.Join(", ", cost.Unknown.Keys.Take(5))) : ""));
    }

    /// <summary>
    /// The save bar in the Workshop: the piece count, how many would fall, whether it is saved, and Save blueprint.
    /// </summary>
    private void WorkshopSaveBar(EditSession s)
    {
        ShowWorkshopCost();
        int n = Workshop.Pieces(s.Scene);
        bool dirty = WorkshopDirty;
        string? falls = LastSupport is { Breaking: > 0 } r ? Lang.T("workshop.bar.falling", r.Breaking) : null;
        PendingText.Text = n == 0 ? Lang.T("workshop.bar.none")
            : string.Join(" · ", new[] { Lang.T("workshop.bar.pieces", n), falls, dirty ? Lang.T("workshop.bar.notSaved") : Lang.T("workshop.bar.saved") }.OfType<string>());
        SaveButton.IsEnabled = n > 0;
        SaveButton.Content = Lang.T("workshop.label.saveBlueprint");
        SaveButton.Tip("top.saveBlueprint");
        SetPendingPill(dirty);
        _liveBadge.IsVisible = false;
        UndoButton.IsEnabled = s.CanUndo;
        RedoButton.IsEnabled = s.CanRedo;
    }

    /// <summary>
    /// After a change Claude made (ClaudeTools): the save bar, the support check, the message (in the user's language,
    /// its English kept for Claude).
    /// </summary>
    /// <param name="message">What the status bar says.</param>
    internal void AfterClaudeEdit(Said message)
    {
        Msg(message);
        UpdateSaveBar();
        QueueSupport();
    }

    /// <summary>
    /// True while a support check is queued.
    /// </summary>
    private bool _supportQueued;

    /// <summary>
    /// After an edit in the Workshop: the support check again (a moment later, once per batch of edits).
    /// </summary>
    private void QueueSupport()
    {
        if (!InWorkshop || _supportQueued)
        {
            return;
        }
        _supportQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _supportQueued = false;
            RefreshSupport();
            if (Session is { } s)
            {
                WorkshopSaveBar(s);
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// What the Workshop changes and gives back when it closes: the Place tool's settings (Build sets them for building
    /// pieces) and the zone borders (the plot is no world).
    /// </summary>
    /// <param name="Chosen">The Place tool's chosen kinds.</param>
    /// <param name="Mode">Its mode.</param>
    /// <param name="OneAtATime">Whether it placed one at a time.</param>
    /// <param name="OneAtATimeByHand">Whether One at a time was set by hand.</param>
    /// <param name="RandomYaw">Whether it turned each object at random.</param>
    /// <param name="Tilt">The most it leaned an object at random, in degrees.</param>
    /// <param name="SizeMin">Its smallest random scale, in percent.</param>
    /// <param name="SizeMax">Its largest random scale, in percent.</param>
    /// <param name="Elevation">Where objects stood.</param>
    /// <param name="SnapTo">Whether pieces snapped to the pieces already there.</param>
    /// <param name="OnTop">Whether a single piece snapped on top of the piece under the cursor.</param>
    /// <param name="Rotation">The turn it added to every object's facing, in degrees.</param>
    /// <param name="Borders">Whether the zone borders were shown.</param>
    private sealed record Kept(List<string> Chosen, PlaceTool.Modes Mode, bool OneAtATime, bool OneAtATimeByHand, bool RandomYaw, float Tilt, float SizeMin,
        float SizeMax, PlaceTool.Elevations Elevation, bool SnapTo, bool OnTop, float Rotation, bool Borders);

    // (The unsaved marks: everything on the plot is new, so they would only cover the building.)

    /// <summary>
    /// The Place tool's settings kept while building; null when not building.
    /// </summary>
    private Kept? _kept;

    /// <summary>
    /// The Place tool's settings, kept while the Workshop's Build (or building in a dungeon) uses it.
    /// </summary>
    private Kept KeepPlaceTool()
    {
        var t = PlaceTool;
        return new Kept([.. t.Chosen], t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation,
            View.IsOverlayShown(Overlays.Layer.Borders));
    }

    /// <summary>
    /// The Place tool as it was kept, out of Build.
    /// </summary>
    private void RestorePlaceTool(Kept k)
    {
        var t = PlaceTool;
        t.Chosen.Clear();
        t.Chosen.AddRange(k.Chosen);
        (t.Mode, t.OneAtATime, t.OneAtATimeByHand, t.RandomYaw, t.Tilt, t.SizeMin, t.SizeMax, t.Elevation, t.SnapTo, t.OnTop, t.Rotation) =
            (k.Mode, k.OneAtATime, k.OneAtATimeByHand, k.RandomYaw, k.Tilt, k.SizeMin, k.SizeMax, k.Elevation, k.SnapTo, k.OnTop, k.Rotation);
        t.GridStep = 0;
        t.Building = false;
        t.HeightNudge = 0;
        t.AimRay = null;
        t.CutY = null;
        PlaceInput.TurnStep = null;
    }
}
