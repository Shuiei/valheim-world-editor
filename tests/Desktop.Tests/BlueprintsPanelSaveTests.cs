using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel's saving: a building is saved under the details given, asked for when saving, shown and searched
/// by its details and tags, and imported through the file picker.
/// </summary>
public class BlueprintsPanelSaveTests
{
    private static readonly string[] Expected1 = ["gate"];
    [AvaloniaFact]
    public async Task SavingAsksForANameAndBeforeReplacingOne()
    {
        using var r = new BlueprintsPanelRun();
        r.W.View.Paste.Clip = BlueprintsPanelRun.Clip("Gate", "woodwall");
        r.B.AskDetails = (_, _) => Task.FromResult<Homestead.Details?>(null);
        Assert.Null(await r.B.Save());
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
        Assert.Equal("Gate", await r.B.Save());
        Assert.True(File.Exists(Path.Combine(r.Homestead, "Gate.png")));
        r.B.Confirm = _ => Task.FromResult(false);
        Assert.Null(await r.B.Save());
        r.B.Confirm = _ => Task.FromResult(true);
        Assert.Equal("Gate", await r.B.Save());
        Assert.Single(r.Listed);
        // Without Homestead in the game, saving says it shows there once installed.
        r.Installed = false;
        Assert.Equal("Gate", await r.B.Save());
        Assert.Contains("not installed in your game yet", r.Said);
    }

    [AvaloniaFact]
    public async Task DetailsCostAndTagsAreShownSearchedAndChanged()
    {
        using var r = new BlueprintsPanelRun();
        r.W.View.Paste.Clip = BlueprintsPanelRun.Clip("Gate", "woodwall", "woodwall", "stone_wall_2x1");
        r.B.AskDetails = (d, cost) => Task.FromResult<Homestead.Details?>(new("Gate", "A gate for the north wall", ["gate", "stone"]));
        Assert.Equal("Gate", await r.B.Save());
        r.Keep("Tower", "woodwall");
        r.B.Toggle(true);
        string texts = SharedInspectorRun.Texts(r.B.List);
        Assert.Contains("Cost: Stone 4 · Wood 4 (needs Stonecutter, Workbench)", texts);
        Assert.Contains("A gate for the north wall", texts);
        Assert.Contains("stone", texts);
        // Found by a tag, by its description, and by both.
        r.B.Search.Text = "stone";
        Assert.Single(r.RowButtons("Paste"));
        r.B.Search.Text = "north gate";
        Assert.Single(r.RowButtons("Paste"));
        r.B.Search.Text = "";
        // Details…: a new name (the file follows, with its picture) and tags.
        r.B.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "North gate", Tags = ["gate"] });
        Assert.True(await r.B.EditDetails(r.Listed.Single(e => e.Name == "Gate")));
        var gate = r.Listed.Single(e => e.Name == "North gate");
        Assert.Equal("North gate.blueprint", Path.GetFileName(gate.Path));
        Assert.NotNull(gate.Picture);
        Assert.Equal(Expected1, gate.Tags);
        Assert.Equal("A gate for the north wall", gate.Description);
        Assert.Equal(3, gate.Pieces);
        // Cancelled: nothing changes.
        r.B.AskDetails = (_, _) => Task.FromResult<Homestead.Details?>(null);
        Assert.False(await r.B.EditDetails(gate));
    }

    [AvaloniaFact]
    public void ImportFileUsesThePicker()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall");
        string file = r.B.Export("hs:Gate.blueprint", "vbuild")!;
        r.B.PickFile = () => Task.FromResult<string?>(null);
        SharedButtons.Click(r.B.ImportButton);
        Assert.Single(r.Listed);
        r.B.PickFile = () => Task.FromResult<string?>(file);
        SharedButtons.Click(r.B.ImportButton);
        Assert.Equal(2, r.Listed.Count);
        Assert.Contains(r.Listed, e => e.Name.StartsWith("Gate", StringComparison.Ordinal) && e.Name != "Gate");
    }

    // SaveBuildingAs: written without a question under the details given (replacing one of that name), with its
    // picture, and said; a folder that cannot be written is said and nothing is saved.
    [AvaloniaFact]
    public void ABuildingIsSavedUnderTheDetailsGiven()
    {
        using var r = new BlueprintsPanelRun();
        var clip = new System.Text.Json.Nodes.JsonObject
        {
            ["objects"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["name"] = "woodwall", ["dx"] = 0, ["dy"] = 1, ["dz"] = 0, ["rx"] = 0, ["ry"] = 0, ["rz"] = 0, ["scale"] = 0 }),
        };
        Assert.Null(r.B.SaveBuildingAs(clip, new Homestead.Details("Hut", "A hut", ["house"])));
        string path = Path.Combine(r.Homestead, "Hut.blueprint");
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(Path.ChangeExtension(path, ".png")));
        Assert.Contains("#Description:A hut", File.ReadAllText(path));
        Assert.StartsWith("Saved the blueprint “Hut” (1 piece(s))", r.Said);
        // Saved again: replaced.
        Assert.Null(r.B.SaveBuildingAs(clip, new Homestead.Details("Hut", "Changed", [])));
        Assert.Contains("#Description:Changed", File.ReadAllText(path));
        // A folder that is a file: not written.
        File.WriteAllText(Path.Combine(r.Dir, "blocked"), "");
        r.B.FindHomestead = () => new Homestead.Status(true, "1.3.2", ["test"], Path.Combine(r.Dir, "blocked"));
        Assert.StartsWith("Could not save the blueprint", r.B.SaveBuildingAs(clip, new Homestead.Details("Other", "", [])));
    }
}
