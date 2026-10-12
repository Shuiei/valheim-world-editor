using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel's rarer ways: an older blueprint's thumbnail shown, moved to Homestead under a numbered name
/// when its own is taken, deleted from the older store, one that cannot be read, and saving into a blueprints folder
/// that cannot be written.
/// </summary>
public class BlueprintsPanelMoreTests
{
    /// <summary>A 1 × 1 PNG picture as a data URL.</summary>
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    [AvaloniaFact]
    public async Task AnOlderBlueprintIsShownMovedUnderANumberedNameAndDeleted()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall");
        string id = r.B.Store.Save("Gate", "OtherWorld", Png, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Gate", "woodwall")));
        r.B.Toggle(true);
        Assert.Contains(r.B.List.GetLogicalDescendants().OfType<Image>(), i => i.Source != null);
        // "Gate" is Homestead's already: numbered.
        Assert.Equal("Gate (2)", r.B.ToHomestead(id));
        Assert.Equal(["Gate", "Gate (2)"], r.Listed.Select(e => e.Name).Order());
        // Gone from the older store once deleted.
        await r.B.Delete(id, "Gate");
        Assert.Empty(r.B.Store.List());
        // One that is not there.
        Assert.Null(r.B.ToHomestead("no-such-blueprint"));
        Assert.Equal("That blueprint could not be read.", r.Said);
    }

    [AvaloniaFact]
    public async Task SavingIntoAFolderThatCannotBeWrittenIsSaidSo()
    {
        using var r = new BlueprintsPanelRun();
        Directory.CreateDirectory(r.Dir);
        File.WriteAllText(r.Homestead, "not a folder");
        r.W.View.Paste.Clip = BlueprintsPanelRun.Clip("Gate", "woodwall");
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
        Assert.Null(await r.B.Save());
        Assert.StartsWith("Could not save the blueprint: ", r.Said);
        var clip = CopyFormat.ToJson(BlueprintsPanelRun.Clip("Tower", "woodwall"));
        Assert.Null(await r.B.SaveBuilding(clip, new Homestead.Details("Tower", "", [])));
        Assert.StartsWith("Could not save the blueprint: ", r.Said);
    }
}
