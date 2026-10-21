using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a kept selection stores: the selected objects that still exist, as kind and place; nothing while the world has no things.
/// </summary>
public class SelectPanelKeptItemsTests
{
    [AvaloniaFact]
    public void KeptItemsAreTheSelectedObjectsAsKindAndPlace()
    {
        var things = new List<WorldScene.Thing> { new(0, 3, new Vector3(1, 2, 3), Vector3.Zero, 1, false), new(1, 4, new Vector3(4, 5, 6), Vector3.Zero, 1, false) };
        var p = new SelectPanel(new GlView().SelectTool) { SelectedIds = () => [1, 7] };
        var items = p.SelectedItems(things);
        var item = Assert.Single(items);
        Assert.Equal(4, item.Prefab);
        Assert.Equal((4f, 5f, 6f), (item.X, item.Y, item.Z));
    }

    [AvaloniaFact]
    public void NothingIsKeptWhileTheWorldHasNoThings()
    {
        var p = new SelectPanel(new GlView().SelectTool) { SelectedIds = () => [0] };
        Assert.Empty(p.SelectedItems(null));
    }
}
