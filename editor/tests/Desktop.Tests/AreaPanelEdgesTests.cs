using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel with nothing open (no heightmap, nothing inside, no picture read, nothing to unmark) and a heightmap
/// that cannot be written where asked.
/// </summary>
public class AreaPanelEdgesTests
{
    [AvaloniaFact]
    public async Task WithoutAnAreaNothingHappens()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        var p = w.AreaPanel;
        Assert.Null(p.ExportHeightmap(Path.GetTempPath()));
        Assert.Empty(p.ThingsInside([new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10)], chosenKindsOnly: false));
        string? said = null;
        p.Message += t => said = t.Shown;
        p.LoadPicture("no-such-picture.png");
        await p.Apply();
        p.UnresetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(said);
    }

    [AvaloniaFact]
    public void AHeightmapThatCannotBeWrittenSaysWhere()
    {
        string file = Path.GetTempFileName();
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            var s = TestScenes.Flat(1);
            w.View.Show(s.Scene, null);
            w.Edit(s);
            // A folder inside a file: it cannot be made.
            string said = w.AreaPanel.ExportHeightmap(Path.Combine(file, "maps"))!.Shown;
            Assert.StartsWith($"Could not write the heightmap to {Path.Combine(file, "maps")}", said);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
