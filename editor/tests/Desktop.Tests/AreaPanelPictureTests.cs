using Avalonia.Headless.XUnit;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's heightmaps: export to a folder, pictures that are not pictures refused, import and put, a picture dropped when the selection changes, and a picture refused for another area.
/// </summary>
[Collection("World files")]
public class AreaPanelPictureTests
{
    [AvaloniaFact]
    public async Task HeightmapsAreExportedToTheFolderAndBadPicturesRefused()
    {
        using var r = new SharedAreaPanelRun();
        string png = r.ExportHeightmap();
        Assert.StartsWith($"Heightmap written to {Path.GetDirectoryName(png)}", r.Said);
        // A picture that is not one.
        r.PickABadPicture(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.Said.StartsWith("That picture cannot be used", StringComparison.Ordinal));
        // The exported one, imported back over the whole area (no selection), then cancelled.
        r.W.View.Area.Clear();
        r.P.PickPicture = () => Task.FromResult<string?>(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
        Click(r.P.CancelPictureButton);
        Click(r.P.PutButton);
        Assert.False(r.S.CanUndo);
        // Again, with the heights pushed above the game's limit: it says how many points stopped short.
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the whole area") == true);
        r.P.LowestBox.Value += 20;
        r.P.HighestBox.Value += 20;
        Click(r.P.PutButton);
        Assert.StartsWith("Imported, but", r.Said);
        Assert.Contains("could not reach their height", r.Said);
    }

    [AvaloniaFact]
    public async Task APictureReadIsDroppedWhenTheSelectionChangesAndRefusedForAnotherArea()
    {
        using var r = new SharedAreaPanelRun();
        string png = r.ExportHeightmap();
        r.P.PickPicture = () => Task.FromResult<string?>(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the selection") == true);
        // The same outline again (the box redrawn where it is, Enter on a polygon that already has its points): the
        // picture stays.
        r.Select(2, 2, 62, 62);
        Assert.DoesNotContain("dropped", r.Said);
        // The selection changes: the picture was fitted to the old one.
        r.Select(2, 2, 30, 30);
        Assert.Equal("The picture read was dropped: the selection changed. Import it again.", r.Said);
        Click(r.P.PutButton);
        Assert.False(r.S.CanUndo);
        // Read again, then another area opens without the selection changing: its grid indices are the old ground's.
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.Contains("the selection") == true);
        var scene = WorldScene.Load(r.Dir, 0, 0, 1);
        r.W.View.Show(scene, null);
        r.W.Edit(scene.Session!);
        Click(r.P.PutButton);
        Assert.Equal("The picture was fitted to another area: import it again.", r.Said);
        Assert.False(scene.Session!.CanUndo);
        Assert.False(r.S.CanUndo);
        // Dropped: putting again does nothing.
        r.W.MessageText.Text = "";
        Click(r.P.PutButton);
        Assert.Equal("", r.Said);
    }
}
