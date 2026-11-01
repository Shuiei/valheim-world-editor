using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The right-hand panels sharing their place (MainWindow.RightPanels): the one chosen comes back after the Inspector.
/// </summary>
public class MainWindowRightPanelsTests
{
    // Every area opened closes the Inspector, which showed the View panel whatever was chosen: with History open (as the
    // editor starts when History was open last), the View panel lay under it; with none, it showed anyway.
    [AvaloniaFact]
    public void TheChosenPanelComesBackWhenTheInspectorCloses()
    {
        var (w, _) = TestWindows.EditingFlat();
        try
        {
            w.ShowRight(w.History.Card);
            w.Inspector.Close();
            Assert.True(w.History.Card.IsVisible);
            Assert.False(w.ViewPanelCard.IsVisible);
            w.ShowRight(null);
            w.Inspector.Close();
            Assert.False(w.ViewPanelCard.IsVisible);
            Assert.False(w.History.Card.IsVisible);
            w.ShowRight(w.ViewPanelCard);
            w.Inspector.Close();
            Assert.True(w.ViewPanelCard.IsVisible);
        }
        finally
        {
            w.Close();
        }
    }

    // The Workshop has its own right-hand panel, Materials: shown at first, closed and opened with its button (kept for
    // the next time), back after the Inspector; the worlds' choice is left as it was.
    [AvaloniaFact]
    public async Task TheWorkshopShowsItsMaterialsAndKeepsTheWorldsChoice()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.ShowRight(w.History.Card);

        await w.OpenWorkshop(null);

        Assert.True(w.MaterialsButton.IsVisible);
        Assert.True(w.Materials.Card.IsVisible);
        Assert.False(w.History.Card.IsVisible);
        Assert.Contains("on", w.MaterialsButton.Classes);
        // Pieces put down: counted at once.
        w.Session!.Commit("A wall", null, [], [(new Editing.NewObject(0, StableHash.Of("woodwall"), new System.Numerics.Vector3(0, Workshop.Ground, 0), System.Numerics.Vector3.Zero, 0f), false)]);
        w.AfterClaudeEdit(Said.Empty);
        Assert.Equal("1 piece(s)", w.Materials.Count.Text);

        w.MaterialsButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Assert.False(w.Materials.Card.IsVisible);
        Assert.False(w.Prefs.Get("workshop.materials", true));
        w.Inspector.Close();
        Assert.False(w.Materials.Card.IsVisible);
        w.ShowMaterials(true);
        w.Inspector.Close();
        Assert.True(w.Materials.Card.IsVisible);

        await w.LeaveWorkshop();

        Assert.False(w.MaterialsButton.IsVisible);
        Assert.False(w.Materials.Card.IsVisible);
        Assert.Equal("history", w.Prefs.Get("panel.right", ""));
    }

    // The right-hand panels stand above the Build dock while it is shown, and back down to the status bar after.
    [AvaloniaFact]
    public void TheRightPanelsStandAboveTheDock()
    {
        var (w, _) = TestWindows.EditingFlat();
        try
        {
            double bottom = w.HelpCard.Margin.Bottom;
            w.BuildPanel.Card.IsVisible = true;
            Assert.True(w.HelpCard.Margin.Bottom > bottom);
            Assert.Equal(w.HelpCard.Margin.Bottom, w.Materials.Card.Margin.Bottom);
            w.BuildPanel.Card.IsVisible = false;
            Assert.Equal(bottom, w.HelpCard.Margin.Bottom);
        }
        finally
        {
            w.Close();
        }
    }

    // The Materials list is made again whenever the card comes back: pieces built while the Inspector stood in its place
    // are counted when the Inspector closes.
    [AvaloniaFact]
    public async Task TheMaterialsCountWhatWasBuiltWhileTheyWereHidden()
    {
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        await w.OpenWorkshop(null);
        Assert.True(w.Materials.Card.IsVisible);
        Assert.Equal("", w.Materials.Count.Text);
        // The Inspector's place: the card hidden, a wall built meanwhile.
        w.ShowRight(w.Inspector.Card);
        Assert.False(w.Materials.Card.IsVisible);
        w.Session!.Commit("A wall", null, [], [(new Editing.NewObject(0, StableHash.Of("woodwall"), new System.Numerics.Vector3(0, Workshop.Ground, 0), System.Numerics.Vector3.Zero, 0f), false)]);
        w.AfterClaudeEdit(Said.Empty);
        Assert.Equal("", w.Materials.Count.Text);

        w.Inspector.Close();

        Assert.True(w.Materials.Card.IsVisible);
        Assert.Equal("1 piece(s)", w.Materials.Count.Text);
    }
}
