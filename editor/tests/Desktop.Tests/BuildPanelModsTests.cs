using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build panel's pieces with a modded game's: learned from a live game, a mod's pieces are listed at once, on the
/// Mods tab whatever their tool (a mod's own hammer too) and category, and the Workshop counts them as built; a
/// mod's hoe pieces are ground edits, not listed. The Mods tab groups them by mod: one mod's at a time, chosen on a row
/// of mods when there are several (hidden on other tabs and while searching), and no more than MaxTiles at once. Runs
/// alone (the catalogues are shared), in a data folder of its own; the tests' copies are in use again afterwards. A
/// mod's worldeditor.json hides pieces from the panel and groups the others under its own headings.
/// </summary>
[Collection("DataDir")]
public sealed class BuildPanelModsTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-buildmods-" + Guid.NewGuid().ToString("N")[..8]);

    public BuildPanelModsTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>
    /// Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.
    /// </summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        ModManifests.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private const string Report = """
        {
          "prefabs": {
            "bfp_finewall": {"p":1,"d":0,"t":2},
            "odin_path": {"p":1,"d":0,"t":2}
          },
          "pieces": {
            "bfp_finewall": {"cat":7,"comfort":0,"tool":"BFP_FineHammer"},
            "odin_path": {"cat":0,"comfort":0,"tool":"hoe"}
          },
          "cost": {"pieces": {"bfp_finewall": {"n":"Fine wood wall","r":[["FineWood",4,1]]}}, "items": {}}
        }
        """;

    [Fact]
    public void AModsPiecesAreListedOnTheModsTabOnceLearned()
    {
        Assert.DoesNotContain(BuildPanel.Pieces, p => p.Category == BuildPanel.Mods);

        GameData.AddMods(Report);

        var pieces = BuildPanel.Pieces;
        Assert.Contains(pieces, p => p.Prefab == "bfp_finewall" && p.Name == "Fine wood wall" && p.Category == BuildPanel.Mods);
        Assert.DoesNotContain(pieces, p => p.Prefab == "odin_path");
        Assert.Contains(BuildPanel.Mods, BuildPanel.Tabs);
        Assert.True(Workshop.Buildable("bfp_finewall"));
        Assert.False(Workshop.Buildable("odin_path"));
    }

    /// <summary>
    /// A report of mod pieces built with a hammer: each prefab with the mod that adds it (null: the game did not say).
    /// </summary>
    private static string ModPieces(params (string Prefab, string? Mod)[] pieces)
    {
        var prefabs = new JsonObject();
        var entries = new JsonObject();
        var mods = new JsonObject();
        foreach (var (prefab, mod) in pieces)
        {
            prefabs[prefab] = new JsonObject { ["p"] = 1, ["d"] = 0, ["t"] = 2 };
            entries[prefab] = new JsonObject { ["cat"] = 2, ["comfort"] = 0, ["tool"] = "Hammer" };
            if (mod != null)
            {
                mods[prefab] = mod;
            }
        }
        return new JsonObject { ["prefabs"] = prefabs, ["pieces"] = entries, ["mods"] = mods }.ToJsonString();
    }

    /// <summary>
    /// A window's Build panel, with its row of tabs and its row of mods.
    /// </summary>
    private static (BuildPanel Panel, WrapPanel Tabs, WrapPanel Mods) Panel()
    {
        var p = new MainWindow(load: false).BuildPanel;
        var rows = ((Panel)p.Search.Parent!).Children.OfType<WrapPanel>().ToList();
        var tabs = rows.Single(r => r.Children.OfType<Button>().Any(b => b.Tag is int));
        return (p, tabs, rows.Single(r => r != tabs));
    }

    /// <summary>A button clicked.</summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>A tab's button.</summary>
    private static Button Tab(WrapPanel tabs, int category) => tabs.Children.OfType<Button>().Single(b => (int)b.Tag! == category);

    /// <summary>The prefabs of the tiles listed.</summary>
    private static List<string> Listed(BuildPanel p) => [.. p.Tiles.Children.OfType<Button>().Select(b => (string)b.Tag!)];

    [Fact]
    public void AModsPiecesAreGroupedUnderItsMod()
    {
        GameData.AddMods(ModPieces(("zzmod_first", "ZetaMod"), ("zzmod_second", "AlphaMod"), ("zzmod_loose", null)));

        Assert.Equal("ZetaMod", BuildPanel.ModGroup("zzmod_first"));
        Assert.Equal("Other mods", BuildPanel.ModGroup("zzmod_loose"));
        Assert.Equal("", BuildPanel.ModGroup("woodwall"));
        // Listed by mod first, whatever their names.
        Assert.Equal(["zzmod_second", "zzmod_loose", "zzmod_first"], BuildPanel.Pieces.Where(p => p.Category == BuildPanel.Mods).Select(p => p.Prefab));
    }

    [AvaloniaFact]
    public void TheModsTabListsOneModAtATime()
    {
        GameData.AddMods(ModPieces(("zzmod_alpha_1", "AlphaMod"), ("zzmod_alpha_2", "AlphaMod"), ("zzmod_zeta", "ZetaMod"), ("zzmod_loose", null)));
        var (p, tabs, mods) = Panel();

        Click(Tab(tabs, BuildPanel.Mods));

        Assert.True(mods.IsVisible);
        var buttons = mods.Children.OfType<Button>().ToList();
        Assert.Equal(["AlphaMod (2)", "Other mods (1)", "ZetaMod (1)"], buttons.Select(b => (string)b.Content!));
        Assert.Contains("on", buttons[0].Classes);
        Assert.Equal(["zzmod_alpha_1", "zzmod_alpha_2"], Listed(p));
        Assert.Contains(p.Tiles.Children.OfType<TextBlock>(), t => t.Text == "AlphaMod · No station needed");
        // Another mod chosen: its pieces.
        Click(buttons[2]);
        Assert.Equal(["zzmod_zeta"], Listed(p));
        Assert.Contains("on", mods.Children.OfType<Button>().Single(b => (string)b.Tag! == "ZetaMod").Classes);
        // Searching: every tab's pieces, no row; the search cleared, the mod chosen again.
        p.Search.Text = "zzmod";
        Assert.False(mods.IsVisible);
        Assert.Equal(4, Listed(p).Count);
        p.Search.Text = "";
        Assert.True(mods.IsVisible);
        Assert.Equal(["zzmod_zeta"], Listed(p));
        // Another tab: no row.
        Click(Tab(tabs, 2));
        Assert.False(mods.IsVisible);
    }

    [AvaloniaFact]
    public void AManifestHidesAPieceAndGroupsTheOthers()
    {
        GameData.AddMods(ModPieces(("zzmod_tool", "AlphaMod"), ("zzmod_rib", "AlphaMod"), ("zzmod_wall", "AlphaMod")));
        string plugins = Path.Combine(_dir, "BepInEx", "plugins", "Author-AlphaMod");
        Directory.CreateDirectory(plugins);
        File.WriteAllText(Path.Combine(plugins, ModManifests.FileName), """
            { "version": 1, "prefabs": { "zzmod_tool": { "hidden": true }, "zzmod_rib": { "group": "Hulls" } } }
            """);
        ModManifests.Refresh(new AppSettings { BepInExFolders = [Path.Combine(_dir, "BepInEx")] });
        var (p, tabs, _) = Panel();

        Click(Tab(tabs, BuildPanel.Mods));

        Assert.DoesNotContain(BuildPanel.Pieces, x => x.Prefab == "zzmod_tool");
        Assert.Equal(["zzmod_wall", "zzmod_rib"], Listed(p));
        var headings = p.Tiles.Children.OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["AlphaMod · No station needed", "AlphaMod · Hulls"], headings);
    }

    [AvaloniaFact]
    public void OneModsPiecesNeedNoRowOfMods()
    {
        GameData.AddMods(ModPieces(("zzmod_alpha_1", "AlphaMod"), ("zzmod_alpha_2", "AlphaMod")));
        var (p, tabs, mods) = Panel();

        Click(Tab(tabs, BuildPanel.Mods));

        Assert.False(mods.IsVisible);
        Assert.Equal(["zzmod_alpha_1", "zzmod_alpha_2"], Listed(p));
    }

    [AvaloniaFact]
    public void NoMoreThanMaxTilesAreListed()
    {
        int all = BuildPanel.MaxTiles + 5;
        GameData.AddMods(ModPieces([.. Enumerable.Range(0, all).Select(i => ($"zzmod_many_{i:D4}", (string?)"ManyMod"))]));
        var (p, tabs, _) = Panel();

        Click(Tab(tabs, BuildPanel.Mods));

        Assert.Equal(BuildPanel.MaxTiles, Listed(p).Count);
        Assert.Contains(p.Tiles.Children.OfType<TextBlock>(), t => t.Text == $"Showing {BuildPanel.MaxTiles} of {all}: search to find the others.");
    }
}
