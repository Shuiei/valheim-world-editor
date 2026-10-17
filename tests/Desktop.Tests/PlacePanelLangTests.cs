using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool in another language (the made-up one: PseudoLang): its panel in every mode and line shape, with
/// nothing, one kind and several kinds chosen, the list of kinds (folded, searched, nothing found), the preset
/// questions, and a message, show only catalogue texts; the message's English is kept for Claude, and what a spawn
/// point spawns is told to Claude in English. The brush line names what a piece snaps to in a whole sentence for each
/// way of snapping.
/// </summary>
[Collection("DataDir")]
public sealed class PlacePanelLangTests
{
    /// <summary>Clicks the button and runs the posted work it starts.</summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Chooses these kinds, as ticking them in the panel does.</summary>
    private static void Choose(PlacePanel p, PlaceTool t, params string[] names)
    {
        t.Chosen.Clear();
        t.Chosen.AddRange(names);
        typeof(PlacePanel).GetMethod("Chosen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(p, [true]);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task PlacePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            var scene = WorldScene.Load(dir, 0, 0, 1);
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            w.Tools.ChooseMode(ToolMode.Place);
            var p = w.PlacePanel;
            var t = w.PlaceTool;
            p.Memory.Presets.Clear();
            p.Memory.Favourites.Clear();
            p.Memory.Recent.Clear();
            // Kind and creature names are data, shown as they are.
            string[] data = [.. p.Creatable().Select(c => c.Name), .. p.Creatable().SelectMany(c => PrefabCatalog.SpawnOf(c.Name)?.Creatures ?? []), "sapling_carrot", "My woods"];
            void Check()
            {
                Dispatcher.UIThread.RunJobs();
                Assert.Empty(PseudoLang.Untranslated(p.Card, data));
                Assert.Empty(PseudoLang.Untranslated(p.Chooser, data));
            }

            // Nothing chosen: the note, the empty mix and the info say so.
            Choose(p, t);
            Check();
            // A kind that grows only on cultivated ground, then several (their shares and sliders), as chips too.
            p.Memory.Favourites.Add("Beech1");
            p.Memory.Recent.Add("Oak1");
            Choose(p, t, "sapling_turnip");
            Check();
            Choose(p, t, "Beech1", "Oak1", "sapling_turnip", "sapling_carrot");
            Check();
            p.ElevationBox.SelectedIndex = 1;
            foreach (var mode in Enum.GetValues<PlaceTool.Modes>())
            {
                Click(p.ModeButtons[mode]);
                Check();
            }
            Click(p.ModeButtons[PlaceTool.Modes.Line]);
            foreach (var shape in p.ShapeButtons.Values)
            {
                Click(shape);
                Check();
            }
            p.SingleBox.IsChecked = true;
            Click(p.ModeButtons[PlaceTool.Modes.Brush]);
            Check();
            p.SingleBox.IsChecked = false;

            // The list of kinds: folded categories, a search showing spawn points and nests, and nothing found.
            Click(p.KindsButton);
            Check();
            p.Search.Text = "Greydwarf";
            Check();
            p.Search.Text = "no kind is called this";
            Check();
            Click(p.KindsButton);

            // A message: shown in the language, its English kept for Claude.
            Click(p.PickButton);
            Assert.Equal(PseudoLang.Wrap("Click an object to pick its kind for placing. Esc cancels."), w.MessageText.Text);
            Assert.Equal("Click an object to pick its kind for placing. Esc cancels.", w.LastMessageEn);
            w.PlaceInput.PickOnce = null;

            // The preset questions: saving asks for a name, deleting one of yours asks first.
            Choose(p, t, "Beech1");
            Click(p.SavePresetButton);
            var d = TestWindows.Shown(w);
            Assert.Empty(PseudoLang.Untranslated(d));
            DialogsTests.Press(d, Lang.T("common.cancel"));
            await TestWindows.Until(() => w.OwnedWindows.Count == 0);
            p.AskName = () => Task.FromResult<string?>("My woods");
            Click(p.SavePresetButton);
            await TestWindows.Until(() => p.DeletePresetButton.IsEnabled);
            Check();
            Click(p.DeletePresetButton);
            d = TestWindows.Shown(w);
            Assert.Empty(PseudoLang.Untranslated(d, "My woods"));
            DialogsTests.Press(d, Lang.T("common.cancel"));
            await TestWindows.Until(() => w.OwnedWindows.Count == 0);
            Assert.Single(p.Memory.Presets);
            p.Memory.Presets.Clear();
            p.Memory.Favourites.Clear();
            p.Memory.Recent.Clear();
            p.Memory.Save();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [Fact]
    public void TheBrushLineSaysWhatThePieceSnapsToInOneSentence()
    {
        using var pseudo = new PseudoLang();
        Assert.Equal("", PlacePanel.SnapNote(null));
        Assert.Equal(PseudoLang.Wrap("Snapped on top of Wood Wall."), PlacePanel.SnapNote((PlaceTool.SnapWays.OnTop, "Wood Wall")));
        Assert.Equal(PseudoLang.Wrap("Snapped beside Wood Wall."), PlacePanel.SnapNote((PlaceTool.SnapWays.Beside, "Wood Wall")));
        Assert.Equal(PseudoLang.Wrap("Snapped to Wood Wall."), PlacePanel.SnapNote((PlaceTool.SnapWays.To, "Wood Wall")));
    }

    [AvaloniaFact]
    public async Task WhatASpawnPointSpawnsIsToldToClaudeInEnglish()
    {
        using var pseudo = new PseudoLang();
        var boar = PrefabCatalog.SpawnOf("Spawner_Boar")!;
        Assert.Equal("Boar · 0–2 ★ · " + PseudoLang.Wrap("spawns once"), boar.Summary());
        var found = ClaudeRun.J(await ClaudeTools.FindPrefabs("boar")).EnumerateArray().ToList();
        var spawner = Assert.Single(found, f => f.GetProperty("prefab").GetString() == "Spawner_Boar");
        Assert.Equal("Boar · 0–2 ★ · spawns once", spawner.GetProperty("spawns").GetString());
        Assert.All(found, f => Assert.DoesNotContain(PseudoLang.Open, f.ToString()));
    }
}
