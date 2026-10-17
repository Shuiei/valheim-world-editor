using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Dungeon panel in another language (the made-up one: PseudoLang): with a Frost Cave in the area and the dungeon
/// its Generate part made (and that part as the Workshop shows it), it shows only catalogue texts and data (rooms',
/// biomes', styles', materials' and bosses' names, the made-up dungeon name); a change the tool makes is said in the
/// language and kept in English for Claude; and Claude's dungeon tools still answer in English.
/// </summary>
[Collection("DataDir")]
public sealed class DungeonPanelLangTests
{
    private static readonly Vector3 At = new(10, 5100, 10);

    /// <summary>The test world's zone 0, 0 open, with a Frost Cave of its entrance room and a copy of it overlapping.</summary>
    private static async Task<ClaudeRun> WithCave()
    {
        var r = new ClaudeRun();
        await r.OpenArea();
        int cave = StableHash.Of("DG_Cave");
        var entrance = new Dungeons.Placed(StableHash.Of("cave_new_entrance02"), At, Quaternion.Identity);
        byte[] blank = ZdoBuilder.Blank(cave, Terrain.PrefabCatalog.Get(cave)!.Flags, At, Vector3.Zero, 0f);
        byte[] data = Dungeons.WithRooms(blank, [entrance, entrance with { Position = At + new Vector3(0, 0.5f, 0) }]);
        r.W.Session!.Commit("A dungeon", null, [], [(new NewObject(0, cave, At, Vector3.Zero, 0, null, false, data), false)]);
        return r;
    }

    /// <summary>
    /// Waits until the generator's newest dungeon is made (its buttons on). A making that failed ends the wait too, and
    /// fails with the panel's reason.
    /// </summary>
    private static async Task Settled(DungeonGenPanel g)
    {
        string failed = Lang.T("dungeon.hint.notMade", "\u0001").Split('\u0001')[0];
        await TestWindows.Until(() => g.Pending.IsCompleted && (g.OpenOnPlot.IsEnabled || g.Summary.Text?.StartsWith(failed, StringComparison.Ordinal) == true));
        Assert.True(g.OpenOnPlot.IsEnabled, g.Summary.Text);
    }

    /// <summary>
    /// The names that are data in the generator's part: biomes, styles, materials and bosses (ids the generator is
    /// given), the made-up name and the rooms of the plan the settings make.
    /// </summary>
    private static IEnumerable<string> GeneratorData(DungeonGenPanel g) =>
        DungeonKit.Biomes.Select(b => b.Name)
            .Concat(DungeonKit.Styles.Select(s => s.Name))
            .Concat(DungeonKit.Materials.Select(m => m.Name))
            .Concat(DungeonKit.Biomes.SelectMany(b => b.Elites.Append(b.Boss)).Select(f => f.Creature))
            .Concat(DungeonGen.Make(g.Settings()).Map!.Select(m => m.Name))
            .Append(g.Name.PlaceholderText ?? "");

    [AvaloniaFact]
    public async Task DungeonPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = await WithCave();
        r.W.Tools.ChooseMode(ToolMode.Dungeon);
        Dispatcher.UIThread.RunJobs();
        var p = r.W.DungeonPanel;
        await Settled(p.Gen);
        // The cave listed and open: its line, its rooms and the overlap of its two rooms.
        Assert.Single((IEnumerable<string>)p.DungeonBox.ItemsSource!);
        Assert.StartsWith(PseudoLang.Open.ToString(), p.Info.Text);
        string[] data = [.. r.W.View.Dungeon.Palette.Select(room => room.Name), .. GeneratorData(p.Gen)];
        Assert.Empty(PseudoLang.Untranslated(p.Card, data));

        // The Workshop's Generate dungeon: its own buttons, under the settings.
        var inWorkshop = new DungeonGenPanel(inWorkshop: true);
        await Settled(inWorkshop);
        Assert.Empty(PseudoLang.Untranslated(inWorkshop.View, [.. GeneratorData(inWorkshop)]));
        Assert.Empty(PseudoLang.Untranslated(inWorkshop.Actions));
    }

    // Close open ends: the status bar in the language, its English kept for Claude.
    [AvaloniaFact]
    public async Task AChangeIsSaidInTheLanguageAndKeptInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = await WithCave();
        r.W.Tools.ChooseMode(ToolMode.Dungeon);
        Dispatcher.UIThread.RunJobs();
        r.W.DungeonPanel.CloseEnds.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.StartsWith(PseudoLang.Open + "Closed ", r.W.MessageText.Text);
        Assert.Matches(@"^Closed \d+ open end\(s\) with end caps\. (.* )?Ctrl\+Z undoes; Save writes it\.$", r.W.LastMessageEn);
    }

    // The kind's name, what a tool did and what a generated dungeon holds reach Claude in English.
    [AvaloniaFact]
    public async Task ClaudeIsToldInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = await WithCave();
        string listed = await r.T.ListDungeons();
        Assert.DoesNotContain(PseudoLang.Open, listed);
        int id = ClaudeRun.J(listed)[0].GetProperty("id").GetInt32();
        Assert.Equal("Frost Cave", ClaudeRun.J(listed)[0].GetProperty("kind").GetString());
        string closed = await r.T.CloseDungeonEnds(id);
        Assert.StartsWith("Closed ", closed);
        Assert.DoesNotContain(PseudoLang.Open, closed);
        // A dungeon generated in an area with none up there yet: placed behind its entrance.
        using var other = new ClaudeRun();
        await other.OpenArea();
        string made = await other.T.GenerateDungeon(size: 1, levels: 1, seed: 3, x: 5, z: 5);
        Assert.Contains("Now pending:", made);
        Assert.StartsWith("Generated ", other.W.LastMessageEn);
        Assert.DoesNotContain(PseudoLang.Open, made);
        // The user saw it in the language.
        Assert.StartsWith(PseudoLang.Open.ToString(), other.W.MessageText.Text);
    }
}
