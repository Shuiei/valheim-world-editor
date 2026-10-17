using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.InspectorControls;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The inspector's control helpers, tested once here so the tests using them can rely on them.
/// </summary>
[Collection("World files")]
public sealed class InspectorControlsTests
{
    [AvaloniaFact]
    public void AddStackPutsTheStackInTheFirstFreeSlotOfTheChest()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        AddStack(r, "Resin", 7);
        var item = Assert.Single(r.I.Items!);
        Assert.Equal(("Resin", 7), (item.Name, item.Stack));
    }
}
