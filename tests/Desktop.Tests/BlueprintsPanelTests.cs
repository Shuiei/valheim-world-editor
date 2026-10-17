using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Blueprints panel through its list: Homestead's blueprints (its folder: a temporary one here) and the editor's
/// older ones, empty, searched, each row's buttons (Paste, Edit, .vbuild, Delete; To Homestead for older ones) and their tip
/// keys, thumbnails that are not pictures, what it says for files that are missing, and whether Homestead is installed.
/// Never the user's own folders.
/// </summary>
public class BlueprintsPanelTests
{
    [AvaloniaFact]
    public void TheRailsBlueprintsIsAToolWithItsPanelOnTheLeft()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Hut", "woodwall");
        SharedButtons.Click(r.W.Tools.BlueprintsButton);
        Assert.Equal(ToolMode.Blueprints, r.W.Tools.Mode);
        Assert.Contains("on", r.W.Tools.BlueprintsButton.Classes);
        Assert.True(r.B.Card.IsVisible);
        // In the left column, next to the rail, where the tools' options are.
        Assert.Same(r.W.Tools.Rail.Parent, r.B.Card.Parent);
        Assert.Contains("Hut", SharedInspectorRun.Texts(r.B.List));
        // Another tool replaces it.
        r.W.Tools.ChooseMode(ToolMode.Path);
        Assert.False(r.B.Card.IsVisible);
        Assert.DoesNotContain("on", r.W.Tools.BlueprintsButton.Classes);
        // The Area tool's Blueprints… opens it too; ✕ goes back to View.
        r.W.AreaPanel.LibraryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ToolMode.Blueprints, r.W.Tools.Mode);
        SharedButtons.Click(r.B.Card.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "✕"));
        Assert.Equal(ToolMode.View, r.W.Tools.Mode);
        Assert.False(r.B.Card.IsVisible);
    }

    [AvaloniaFact]
    public void AnEmptyListAndASearchThatFindsNothingSaySo()
    {
        using var r = new BlueprintsPanelRun();
        r.B.Toggle(true);
        Assert.Contains("No blueprints yet", SharedInspectorRun.Texts(r.B.List));
        r.Keep("Gate", "woodwall");
        r.Keep("Tower", "woodwall");
        r.B.Refresh();
        Assert.Equal(2, r.RowButtons("Paste").Count);
        r.B.Search.Text = "tow";
        Assert.Single(r.RowButtons("Paste"));
        r.B.Search.Text = "castle";
        Assert.Contains("Nothing matches.", SharedInspectorRun.Texts(r.B.List));
        // Closed, the list is not read.
        r.B.Toggle(false);
        Assert.False(r.B.Card.IsVisible);
    }

    [AvaloniaFact]
    public void TheBannerSaysWhetherHomesteadIsInstalled()
    {
        using var r = new BlueprintsPanelRun();
        r.B.Toggle(true);
        Assert.Contains("Homestead 1.3.2 is installed (test profile)", r.B.Banner.Text);
        Assert.False(r.B.GetHomesteadButton.IsVisible);
        r.Installed = false;
        r.B.Refresh();
        Assert.StartsWith("Homestead is not installed", r.B.Banner.Text);
        Assert.True(r.B.GetHomesteadButton.IsVisible);
        Uri? opened = null;
        r.B.OpenUrl = u => { opened = u; return Task.FromResult(true); };
        SharedButtons.Click(r.B.GetHomesteadButton);
        Assert.Equal(Homestead.PageUrl, opened?.ToString());
    }

    [AvaloniaFact]
    public void EachRowPastesEditsExportsAndDeletes()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        r.B.Toggle(true);
        Assert.Contains("2 piece(s) · by test", SharedInspectorRun.Texts(r.B.List));
        SharedButtons.Click(r.RowButtons(".vbuild").Single());
        Assert.StartsWith("Written to ", r.Said);
        Assert.EndsWith(".vbuild. Only the objects are written, not the ground.", r.Said);
        SharedButtons.Click(r.RowButtons("Paste").Single());
        Assert.Equal("Gate", r.W.View.Paste.Clip!.Name);
        Assert.False(r.B.Card.IsVisible);
        // Delete asks first, and takes the picture too.
        r.B.Toggle(true);
        r.B.Confirm = _ => Task.FromResult(false);
        SharedButtons.Click(r.RowButtons("Delete").Single());
        Assert.Single(r.Listed);
        r.B.Confirm = _ => Task.FromResult(true);
        SharedButtons.Click(r.RowButtons("Delete").Single());
        Assert.Empty(r.Listed);
        Assert.Empty(Directory.GetFiles(r.Homestead));
        Assert.Contains("No blueprints yet", SharedInspectorRun.Texts(r.B.List));
    }

    // Each row's buttons carry their tip key, which Claude knows them by whatever the language they are shown in.
    [AvaloniaFact]
    public void EachRowsButtonsHaveTipKeys()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        r.B.Store.Save("Old", "OtherWorld", null, CopyFormat.ToJson(BlueprintsPanelRun.Clip("Old", "woodwall")));
        r.B.Toggle(true);
        var keys = r.B.List.GetLogicalDescendants().OfType<Button>().Select(b => $"{b.Content} {Tips.KeyOf(b)}").ToList();
        Assert.Equal(
        [
            "Paste blueprints.paste", "Edit blueprints.edit", "Details blueprints.details", ".vbuild blueprints.vbuild", "Delete blueprints.delete",
            "Paste blueprints.paste", "To Homestead blueprints.toHomestead", ".vbuild blueprints.vbuildOld", "Delete blueprints.deleteOld",
        ], keys);
    }

    // Edit asks for the blueprint's file (the window opens it in the Workshop). Alone in its test: the
    // Workshop opens in the background and changes the tools, which steps after it would meet half way.
    [AvaloniaFact]
    public void EditAsksForTheBlueprintsFile()
    {
        using var r = new BlueprintsPanelRun();
        r.Keep("Gate", "woodwall", "wood_floor");
        r.B.Toggle(true);
        string? edit = "none";
        r.B.EditAsked += p => edit = p;
        SharedButtons.Click(r.RowButtons("Edit").Single());
        Assert.Equal(Path.Combine(r.Homestead, "Gate.blueprint"), edit);
    }

    [AvaloniaFact]
    public void OlderEditorBlueprintsAreListedAndMovedToHomestead()
    {
        using var r = new BlueprintsPanelRun();
        r.B.Store.Save("Gate", "OtherWorld", "data:image/png;base64,not-base64!!", CopyFormat.ToJson(BlueprintsPanelRun.Clip("Gate", "woodwall", "Beech1")));
        r.B.Toggle(true);
        Assert.Contains("Older editor blueprints", SharedInspectorRun.Texts(r.B.List));
        Assert.Contains("from OtherWorld", SharedInspectorRun.Texts(r.B.List));
        // The thumbnail that is not a picture is left empty.
        Assert.Null(r.B.List.GetLogicalDescendants().OfType<Image>().Single().Source);
        SharedButtons.Click(r.RowButtons("To Homestead").Single());
        var moved = Assert.Single(r.Listed);
        Assert.Equal("Gate", moved.Name);
        Assert.NotNull(moved.Picture);
        Assert.Contains("now a Homestead blueprint", r.Said);
        // Its ground (the copy had some) is not kept.
        Assert.Contains("ground shape is not kept", r.Said);
    }
}
