using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build panel as a dock across the bottom of the view: its height kept within bounds and between runs, dragged
/// from its top edge, folded down to its row of tabs and opened again, and the parts of its top row that go with the
/// tab shown (the search and the hammer's tabs with the pieces, the Library's own row with the blueprints).
/// </summary>
public sealed class BuildPanelDockTests
{
    /// <summary>A button clicked.</summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void TheHeightIsKeptWithinItsBoundsAndRemembered()
    {
        var prefs = Prefs.InMemory();
        var p = new MainWindow(load: false).BuildPanel;
        p.Remember(prefs);
        Assert.Equal(BuildPanel.DefaultHeight, p.BodyHeight);

        p.SetBodyHeight(10);
        Assert.Equal(BuildPanel.MinHeight, p.BodyHeight);
        p.SetBodyHeight(5000);
        Assert.Equal(BuildPanel.MaxHeight, p.BodyHeight);
        p.SetBodyHeight(300);
        Assert.Equal(300, prefs.Get("build.dockHeight", 0.0));

        // Another window with the same settings: the same height; a height out of bounds kept is brought back in.
        var again = new MainWindow(load: false).BuildPanel;
        again.Remember(prefs);
        Assert.Equal(300, again.BodyHeight);
        prefs.Set("build.dockHeight", 1.0);
        again.Remember(prefs);
        Assert.Equal(BuildPanel.MinHeight, again.BodyHeight);
    }

    [AvaloniaFact]
    public void FoldingLeavesOnlyTheRowOfTabsAndIsRemembered()
    {
        var prefs = Prefs.InMemory();
        var p = new MainWindow(load: false).BuildPanel;
        p.Remember(prefs);
        Assert.False(p.Folded);
        Assert.Equal("▾", p.CollapseButton.Content);

        Click(p.CollapseButton);

        Assert.True(p.Folded);
        Assert.Equal("▴", p.CollapseButton.Content);
        Assert.False(p.Tiles.FindLogicalAncestorOfType<Grid>()!.FindLogicalAncestorOfType<Grid>()!.FindLogicalAncestorOfType<Grid>()!.IsVisible);
        Assert.False(p.RecentRow.FindLogicalAncestorOfType<Grid>()!.IsVisible);
        Assert.True(prefs.Get("build.dockFolded", false));
        // Folded in the last run: folded again.
        var again = new MainWindow(load: false).BuildPanel;
        again.Remember(prefs);
        Assert.True(again.Folded);
        Click(again.CollapseButton);
        Assert.False(again.Folded);
        Assert.True(again.RecentRow.FindLogicalAncestorOfType<Grid>()!.IsVisible);
    }

    [AvaloniaFact]
    public void TheTopRowShowsWhatGoesWithTheTab()
    {
        var p = new MainWindow(load: false).BuildPanel;
        Assert.True(p.Search.IsVisible);
        Assert.False(p.LibrarySearch.Parent is Control { IsVisible: true });
        Assert.Contains("on", p.PiecesTab.Classes);

        Click(p.LibraryTab);

        Assert.True(p.LibraryShown);
        Assert.False(p.Search.IsVisible);
        Assert.True(((Control)p.LibrarySearch.Parent!).IsVisible);
        Assert.False(((Control)p.HammerTabs.Parent!.Parent!).IsVisible);
        // No foot with the blueprints: the pieces picked last are for building.
        Assert.False(p.RecentRow.FindLogicalAncestorOfType<Grid>()!.IsVisible);
        Assert.Contains("on", p.LibraryTab.Classes);

        Click(p.PiecesTab);

        Assert.False(p.LibraryShown);
        Assert.True(p.Search.IsVisible);
        Assert.True(p.RecentRow.FindLogicalAncestorOfType<Grid>()!.IsVisible);
    }

    [AvaloniaFact]
    public void TheTopEdgeDraggedMakesTheDockTaller()
    {
        var prefs = Prefs.InMemory();
        var p = new MainWindow(load: false).BuildPanel;
        p.Remember(prefs);
        p.Fold(true);
        var grip = p.Card.GetLogicalDescendants().OfType<Border>().First(b => b.Cursor != null && b.Height == 8);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var left = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        var none = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);

        // A move before any press does nothing.
        grip.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, grip, pointer, grip, new Avalonia.Point(0, 0), 0, none, KeyModifiers.None));
        Assert.True(p.Folded);
        // A right press does not start a drag.
        grip.RaiseEvent(new PointerPressedEventArgs(grip, pointer, grip, new Avalonia.Point(0, 100), 0, new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None, 1));
        grip.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, grip, pointer, grip, new Avalonia.Point(0, 0), 0, none, KeyModifiers.None));
        Assert.True(p.Folded);

        // Pressed folded, dragged 50 px up: open, and that much taller than the least height.
        grip.RaiseEvent(new PointerPressedEventArgs(grip, pointer, grip, new Avalonia.Point(0, 100), 0, left, KeyModifiers.None, 1));
        grip.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, grip, pointer, grip, new Avalonia.Point(0, 50), 0, left, KeyModifiers.None));
        Assert.False(p.Folded);
        Assert.Equal(BuildPanel.MinHeight + 50, p.BodyHeight);
        grip.RaiseEvent(new PointerReleasedEventArgs(grip, pointer, grip, new Avalonia.Point(0, 50), 0, none, KeyModifiers.None, MouseButton.Left));
        // Released: moving no longer resizes it.
        grip.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, grip, pointer, grip, new Avalonia.Point(0, -200), 0, none, KeyModifiers.None));
        Assert.Equal(BuildPanel.MinHeight + 50, p.BodyHeight);
        Assert.Equal(BuildPanel.MinHeight + 50, prefs.Get("build.dockHeight", 0.0));
    }

    [AvaloniaFact]
    public void TheToolbarComesAndGoesWithTheDock()
    {
        var p = new MainWindow(load: false).BuildPanel;
        Assert.False(p.Toolbar.IsVisible);
        p.Card.IsVisible = true;
        Assert.True(p.Toolbar.IsVisible);
        Assert.Contains(p.SnapModes, p.Toolbar.GetLogicalDescendants());
        Assert.Contains(p.GridBox, p.Toolbar.GetLogicalDescendants());
        Assert.Contains(p.TurnBox, p.Toolbar.GetLogicalDescendants());
        Assert.Contains(p.LiftReset, p.Toolbar.GetLogicalDescendants());
        p.Card.IsVisible = false;
        Assert.False(p.Toolbar.IsVisible);
    }
}
