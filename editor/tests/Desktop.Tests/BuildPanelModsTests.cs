using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Build panel's pieces with a modded game's: learned from a live game, a mod's pieces are listed at once, on the
/// Mods tab whatever their tool (a mod's own hammer too) and category, and the Workshop counts them as built; a
/// mod's hoe pieces are ground edits, not listed. The Mod pieces tab groups them by mod: one mod's at a time, chosen in
/// the list of mods down its side (hidden on the hammer's tabs and while searching), and no more than MaxTiles at once;
/// with none known, it says how the editor learns them. Runs
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
        ModScan.Forget();
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
    /// A window's Build panel, with its list of mods.
    /// </summary>
    private static (BuildPanel Panel, StackPanel Mods) Panel()
    {
        var p = new MainWindow(load: false).BuildPanel;
        return (p, p.ModList);
    }

    /// <summary>A button clicked.</summary>
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>A hammer tab's button.</summary>
    private static Button Tab(BuildPanel p, int category) => p.HammerTabs.Children.OfType<Button>().Single(b => (int)b.Tag! == category);

    /// <summary>The prefabs of the tiles listed.</summary>
    private static List<string> Listed(BuildPanel p) => [.. p.Tiles.Children.OfType<Button>().Select(b => (string)b.Tag!)];

    /// <summary>A mod's button's words: its name and its number of pieces.</summary>
    private static string Words(Button b) => string.Join(" ", b.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

    [AvaloniaFact]
    public void TheModsTabListsOneModAtATime()
    {
        GameData.AddMods(ModPieces(("zzmod_alpha_1", "AlphaMod"), ("zzmod_alpha_2", "AlphaMod"), ("zzmod_zeta", "ZetaMod"), ("zzmod_loose", null)));
        var (p, mods) = Panel();

        Click(p.ModsTab);

        Assert.True(mods.IsVisible);
        Assert.Contains("on", p.ModsTab.Classes);
        Assert.DoesNotContain("on", p.PiecesTab.Classes);
        // The hammer's tabs are not for the mods' pieces.
        Assert.False(((Control)p.HammerTabs.Parent!.Parent!).IsVisible);
        var buttons = mods.Children.OfType<Button>().ToList();
        Assert.Equal(["AlphaMod 2", "Other mods 1", "ZetaMod 1"], buttons.Select(Words));
        Assert.Contains("on", buttons[0].Classes);
        Assert.Equal(["zzmod_alpha_1", "zzmod_alpha_2"], Listed(p));
        Assert.Contains(p.Tiles.Children.OfType<TextBlock>(), t => t.Text == "AlphaMod · No station needed");
        // Another mod chosen: its pieces.
        Click(buttons[2]);
        Assert.Equal(["zzmod_zeta"], Listed(p));
        Assert.Contains("on", mods.Children.OfType<Button>().Single(b => (string)b.Tag! == "ZetaMod").Classes);
        // Searching: every tab's pieces, no list of mods; the search cleared, the mod chosen again.
        p.Search.Text = "zzmod";
        Assert.False(mods.IsVisible);
        Assert.Equal(4, Listed(p).Count);
        p.Search.Text = "";
        Assert.True(mods.IsVisible);
        Assert.Equal(["zzmod_zeta"], Listed(p));
        // Back to the hammer's pieces: no list of mods, the hammer's tabs again.
        Click(p.PiecesTab);
        Assert.False(mods.IsVisible);
        Assert.True(((Control)p.HammerTabs.Parent!.Parent!).IsVisible);
        Assert.Contains("on", Tab(p, 2).Classes);
    }

    [AvaloniaFact]
    public void OneModsPiecesAreListedUnderItsName()
    {
        GameData.AddMods(ModPieces(("zzmod_alpha_1", "AlphaMod"), ("zzmod_alpha_2", "AlphaMod")));
        var (p, mods) = Panel();

        Click(p.ModsTab);

        Assert.True(mods.IsVisible);
        Assert.Equal(["AlphaMod 2"], mods.Children.OfType<Button>().Select(Words));
        Assert.Equal(["zzmod_alpha_1", "zzmod_alpha_2"], Listed(p));
        Assert.False(p.NoMods.IsVisible);
    }

    [AvaloniaFact]
    public void NoModsPiecesSayHowTheEditorLearnsThem()
    {
        var (p, mods) = Panel();

        Click(p.ModsTab);

        Assert.True(p.NoMods.IsVisible);
        Assert.False(mods.IsVisible);
        Assert.Empty(p.Tiles.Children);
        // A search still finds the game's pieces.
        p.Search.Text = "wall";
        Assert.False(p.NoMods.IsVisible);
        Assert.NotEmpty(Listed(p));
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
        var (p, _) = Panel();

        Click(p.ModsTab);

        Assert.DoesNotContain(BuildPanel.Pieces, x => x.Prefab == "zzmod_tool");
        Assert.Equal(["zzmod_wall", "zzmod_rib"], Listed(p));
        var headings = p.Tiles.Children.OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["AlphaMod · No station needed", "AlphaMod · Hulls"], headings);
    }

    [AvaloniaFact]
    public void NoMoreThanMaxTilesAreListed()
    {
        int all = BuildPanel.MaxTiles + 5;
        GameData.AddMods(ModPieces([.. Enumerable.Range(0, all).Select(i => ($"zzmod_many_{i:D4}", (string?)"ManyMod"))]));
        var (p, _) = Panel();

        Click(p.ModsTab);

        Assert.Equal(BuildPanel.MaxTiles, Listed(p).Count);
        Assert.Contains(p.Tiles.Children.OfType<TextBlock>(), t => t.Text == $"Showing {BuildPanel.MaxTiles} of {all}: search to find the others.");
    }

    // The installed mods' pieces, read without a live game, are listed under their mod beside those a live game told of.
    [AvaloniaFact]
    public void AnInstalledModsPiecesAreListedUnderItsName()
    {
        GameData.AddMods(ModPieces(("zzmod_alpha_1", "AlphaMod")));
        var data = ModScan.Data.Empty;
        ModScan.Take(data, new Dictionary<string, string>
        {
            ["prefabs"] = """{"zzmod_offline":{"p":1,"d":0,"t":2}}""",
            ["pieces"] = """{"zzmod_offline":{"cat":2,"comfort":0,"snap":[[0,0,0],[2,0,0]]},"zzmod_loose":{"cat":2,"comfort":0,"snap":[[0,0,0]],"loose":1}}""",
        }, "Installed Mod");
        GameData.UseModScan(data);
        var (p, mods) = Panel();

        Click(p.ModsTab);

        Assert.Equal(["AlphaMod 1", "Installed Mod 2"], mods.Children.OfType<Button>().Select(Words));
        Click(mods.Children.OfType<Button>().Last());
        Assert.Equal(["zzmod_loose", "zzmod_offline"], Listed(p).Order(StringComparer.Ordinal));
        Assert.Equal("Installed Mod", BuildPanel.ModGroup("zzmod_offline"));
        // No recipe known offline: its cost is unknown, not free.
        p.Choose("zzmod_offline");
        Assert.Equal("Cost unknown (pieces from mods?)", p.Cost.Text);
        Assert.Equal("zzmod_offline · Installed Mod", p.Chosen.Text);
    }

    // While the installed mods are read the tab says so and Rescan mods waits; done, the pieces are listed again.
    [AvaloniaFact]
    public void TheTabSaysWhileTheModsAreReadAndRescanAsksForIt()
    {
        var (p, _) = Panel();
        int asked = 0;
        p.RescanAsked += () => asked++;
        Click(p.ModsTab);
        Assert.True(p.RescanButton.IsVisible);
        Assert.True(p.RescanButton.FindLogicalAncestorOfType<DockPanel>()!.IsVisible);

        p.ShowScanning(true);

        Assert.True(p.ScanningMods);
        Assert.False(p.RescanButton.IsEnabled);
        Assert.Equal("Reading your mods' pieces…", p.NoMods.Text);
        p.ShowScanning(false);
        Assert.True(p.RescanButton.IsEnabled);
        Assert.StartsWith("No piece from a mod found.", p.NoMods.Text);
        Click(p.RescanButton);
        Assert.Equal(1, asked);
        // Searching, or on the hammer's tabs: no column of mods.
        p.Search.Text = "wall";
        Assert.False(p.RescanButton.FindLogicalAncestorOfType<DockPanel>()!.IsVisible);
    }
}
