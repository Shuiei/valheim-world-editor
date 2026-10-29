using Avalonia.Headless.XUnit;
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
}
