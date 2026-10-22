using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The area size list (MainWindow.AreaSize): what the status bar says while the bigger area opens, and after.
/// </summary>
[Collection("World files")]
public class MainWindowAreaSizeTests
{
    // "Opening 3 × 3 zones…" stayed in the status bar once the area had opened, as if it were still opening.
    [AvaloniaFact]
    public async Task TheOpeningLineGoesOnceTheAreaIsOpen()
    {
        using var r = new WorldWindowRun();
        await r.Open();
        await r.W.ResizeArea(3);
        Assert.Equal(3, r.W.View.Scene!.ZonesX);
        Assert.DoesNotContain("Opening", r.W.MessageText.Text ?? "", StringComparison.Ordinal);
    }
}
