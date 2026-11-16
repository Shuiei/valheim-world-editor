using Avalonia.Controls;

namespace TerrainEditor.Desktop;

// The building bar (BuildingBar): one bar of building settings for Build, Select and Paste, beside the tool rail.
public sealed partial class MainWindow
{
    /// <summary>
    /// The building bar: how pieces snap, the grid, the turn and lift steps and who they are built by, shared by Build,
    /// Select and Paste.
    /// </summary>
    internal BuildingBar BuildingBar { get; } = new();

    /// <summary>
    /// Built by, on the building bar: who new pieces are built by (the player ids: the list's entries).
    /// </summary>
    internal ComboBox BuilderBox => BuildingBar.BuilderBox;

    /// <summary>
    /// Wires the building bar: Select and Paste take its settings as they change (Build takes them itself), Reset takes
    /// a paste's height back, the bar shows a paste's height, and Built by sets the builder.
    /// </summary>
    private void WireBuildingBar()
    {
        BuildingBar.Changed += TakeBuildingBar;
        BuildingBar.LiftResetAsked += () =>
        {
            if (Tools.Mode == ToolMode.Paste)
            {
                PastePanel.OffsetBox.Value = 0;
            }
        };
        View.Paste.Changed += ShowPasteLift;
        BuilderBox.SelectionChanged += async (_, _) => await BuilderChosen();
        TakeBuildingBar();
    }

    /// <summary>The building bar's settings given to the Select and Paste tools.</summary>
    private void TakeBuildingBar()
    {
        var select = View.SelectTool;
        select.SnapToPieces = BuildingBar.Snaps;
        select.GridStep = BuildingBar.GridStep;
        select.TurnStep = BuildingBar.TurnStep;
        select.LiftStep = BuildingBar.LiftStep;
        View.Paste.GridStep = BuildingBar.GridStep;
    }

    /// <summary>Pasting: the building bar shows the paste's height.</summary>
    private void ShowPasteLift()
    {
        if (BuildingBar.Use == BuildingBar.Uses.Paste)
        {
            BuildingBar.ShowLift(View.Paste.Offset);
        }
    }

    /// <summary>
    /// The building bar shown for the tool in use (Build, Select or Paste: the parts each uses), hidden with the others.
    /// Built by is offered in a world, not on the Workshop's plot (a blueprint keeps no builder).
    /// </summary>
    private void ShowBuildingBar()
    {
        var use = BuildPanel.Card.IsVisible ? BuildingBar.Uses.Build
            : Tools.SelectMode ? BuildingBar.Uses.Select
            : Tools.Mode == ToolMode.Paste ? BuildingBar.Uses.Paste
            : BuildingBar.Uses.None;
        BuildingBar.Show(use, builder: !InWorkshop);
        if (use == BuildingBar.Uses.Paste)
        {
            ShowPasteLift();
        }
        else
        {
            BuildPanel.ShowLift();
        }
    }

    /// <summary>
    /// Keeps the building bar within the window: between the tool's panel and the right-hand panel it wraps onto a
    /// second row when one is too wide.
    /// </summary>
    /// <param name="tools">The tool column, which holds the bar.</param>
    private void KeepBuildingBarInView(Panel tools) => tools.LayoutUpdated += (_, _) =>
    {
        if (!BuildingBar.Card.IsVisible || _editorPage == null)
        {
            return;
        }
        double right = new[] { ViewPanelCard, History.Card, Inspector.Card, HelpCard, Materials.Card }
            .Where(c => c is { IsVisible: true }).Select(c => c.Bounds.Width + Ui.Gap).DefaultIfEmpty(0).Max();
        BuildingBar.Fit(_editorPage.Bounds.Width - tools.Bounds.X - BuildingBar.Card.Bounds.X - right - 10);
    };
}
