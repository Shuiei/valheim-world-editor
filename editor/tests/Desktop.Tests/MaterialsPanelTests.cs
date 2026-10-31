using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop's Materials panel: a word when the plot is bare; every material with its amount, most first, and the
/// stations needed; the pieces with no known recipe and those only the editor can place, listed apart; its close
/// button.
/// </summary>
[Collection("DataDir")]
public sealed class MaterialsPanelTests : IDisposable
{
    private readonly string? _dataDir = AppSettings.DataDirOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-materials-" + Guid.NewGuid().ToString("N")[..8]);

    public MaterialsPanelTests()
    {
        AppSettings.DataDirOverride = _dir;
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
    }

    /// <summary>Forgets the mods, puts the data folder and the tests' copies back, and deletes the folder.</summary>
    public void Dispose()
    {
        ModCatalogue.Forget();
        GameData.UseCopies(Fixtures.Root);
        AppSettings.DataDirOverride = _dataDir;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    /// <summary>Every text the panel's list shows, in order.</summary>
    private static List<string> Texts(MaterialsPanel m) => [.. m.Rows.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    [AvaloniaFact]
    public void ABarePlotSaysSo()
    {
        var m = new MaterialsPanel();
        Assert.Equal(["Nothing on the plot yet: what you build is counted here."], Texts(m));
        Assert.Equal("", m.Count.Text);
    }

    [AvaloniaFact]
    public void TheMaterialsAndStationsAreListedMostFirst()
    {
        var m = new MaterialsPanel();

        m.Show(new Dictionary<string, int> { ["woodwall"] = 3, ["stone_wall_2x1"] = 4 });

        var texts = Texts(m);
        Assert.Equal("7 piece(s)", m.Count.Text);
        Assert.Equal("MATERIALS", texts[0]);
        int stone = texts.IndexOf("Stone"), wood = texts.IndexOf("Wood");
        Assert.True(stone > 0 && wood > 0);
        // Stone 16 (4 × 4) before Wood 6 (3 × 2).
        Assert.True(stone < wood);
        Assert.Equal("16", texts[stone + 1]);
        Assert.Equal("6", texts[wood + 1]);
        Assert.Contains("STATIONS NEEDED NEARBY", texts);
        Assert.Contains("Stonecutter", texts);
        Assert.Contains("Workbench", texts);
        Assert.DoesNotContain("NO KNOWN RECIPE", texts);
    }

    [AvaloniaFact]
    public void PiecesWithoutARecipeAndEditorOnlyPiecesAreListedApart()
    {
        // A mod's hammer piece the game told no recipe of.
        GameData.AddMods("""
            {
              "prefabs": { "zzmod_norecipe": {"p":1,"d":0,"t":2} },
              "pieces": { "zzmod_norecipe": {"cat":2,"comfort":0,"tool":"Hammer"} },
              "mods": { "zzmod_norecipe": "AlphaMod" }
            }
            """);
        var m = new MaterialsPanel();

        m.Show(new Dictionary<string, int> { ["woodwall"] = 1, ["zzmod_norecipe"] = 2, ["zz_not_a_piece"] = 3 });

        var texts = Texts(m);
        int head = texts.IndexOf("NO KNOWN RECIPE");
        Assert.True(head > 0);
        Assert.Equal("AlphaMod", texts[head + 1]);
        Assert.Equal("zzmod_norecipe", texts[head + 2]);
        Assert.Equal("2", texts[head + 3]);
        int editorOnly = texts.IndexOf("ONLY THE EDITOR CAN PLACE");
        Assert.True(editorOnly > head);
        Assert.Equal("zz_not_a_piece", texts[editorOnly + 1]);
        Assert.Equal("3", texts[editorOnly + 2]);
        Assert.True(MaterialsPanel.NoRecipe("zzmod_norecipe"));
        Assert.False(MaterialsPanel.NoRecipe("woodwall"));
    }

    [AvaloniaFact]
    public void TheCloseButtonHidesTheCardAndSaysSo()
    {
        var m = new MaterialsPanel();
        m.Card.IsVisible = true;
        bool closed = false;
        m.Closed += () => closed = true;

        m.Card.GetLogicalDescendants().OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(m.Card.IsVisible);
        Assert.True(closed);
    }
}
