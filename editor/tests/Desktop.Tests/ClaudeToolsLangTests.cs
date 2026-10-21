using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's edits as the user sees them, in another language (the made-up one: PseudoLang): the status bar shows the
/// language while its English is kept for Claude; the history step keeps its "Claude: " mark before the step's words in
/// the language, so it is still found as Claude's; a path's message, which is also Claude's answer, answers Claude in
/// English. So for every kind of edit: objects, the ground, the world's zones, blueprints, dungeons and the ready
/// tasks; and Take back Claude's changes still finds Claude's steps.
/// </summary>
[Collection("DataDir")]
public sealed class ClaudeToolsLangTests
{
    [AvaloniaFact]
    public async Task ClaudesEditsShowInTheLanguageAndStayClaudes()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        using var pseudo = new PseudoLang();

        string answer = await r.T.ShapeGround("smooth", -20, -20, 20, 20);
        Assert.DoesNotContain("⟦", answer, StringComparison.Ordinal);
        Assert.StartsWith("⟦Claude's " + PseudoLang.Wrap("smooth") + ": ", r.W.MessageText.Text, StringComparison.Ordinal);
        Assert.Matches(@"^Claude's smooth: \d+ ground point\(s\); Ctrl\+Z takes it back\.$", r.W.LastMessageEn);
        Assert.Equal("Claude: " + PseudoLang.Wrap("smooth"), s.UndoLabel);
        Assert.True(HistoryPanel.ByClaude(s.UndoList[^1]));

        answer = await r.T.DigPath("river", [[-20f, 0f], [20f, 0f]], width: 6, depth: 3);
        Assert.DoesNotContain("⟦", answer, StringComparison.Ordinal);
        Assert.StartsWith("⟦", r.W.MessageText.Text, StringComparison.Ordinal);
        Assert.StartsWith(r.W.LastMessageEn + " Now pending: ", answer, StringComparison.Ordinal);
        Assert.Equal("Claude: " + PseudoLang.Wrap("river along a line"), s.UndoLabel);
        Assert.True(HistoryPanel.ByClaude(s.UndoList[^1]));
    }
    /// <summary>
    /// What the user saw of Claude's last edit: Claude's <paramref name="answer"/> in English, the status bar in the
    /// language with its English (kept for Claude) starting with <paramref name="english"/>, and the History's newest
    /// step named <paramref name="step"/>, found as Claude's.
    /// </summary>
    private static void Shown(ClaudeRun r, string answer, string english, string step)
    {
        Assert.DoesNotContain(PseudoLang.Open, answer);
        Assert.StartsWith(english, r.W.LastMessageEn, StringComparison.Ordinal);
        Assert.StartsWith(PseudoLang.Open.ToString(), r.W.MessageText.Text, StringComparison.Ordinal);
        Assert.True(PseudoLang.Translated(r.W.MessageText.Text!, []), r.W.MessageText.Text);
        Assert.Equal(step, r.W.Session!.UndoLabel);
        Assert.True(HistoryPanel.ByClaude(r.W.Session.UndoList[^1]));
    }

    /// <summary>A step of Claude's named with the catalogue's words: "Claude: " and the words in the language.</summary>
    private static string Step(string english) => "Claude: " + PseudoLang.Wrap(english);

    // Objects put down, moved, placed exactly, replaced, filled, changed, copied (once, twice) and taken away: each said
    // in the language, Claude's own words kept as the step's name where Claude gave them; then Take back Claude's
    // changes takes every one of them back and keeps the user's own.
    [AvaloniaFact]
    public async Task ObjectEditsShowInTheLanguageAndAreTakenBackAsClaudes()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        using var pseudo = new PseudoLang();
        static ClaudeTools.PieceSpec Piece(string prefab, float x, float z) => new() { Prefab = prefab, X = x, Z = z, Snap = false };
        string put = await r.T.PlacePieces(
        [
            Piece("wood_pole2", 3, 3), Piece("wood_pole2", -3, 3), Piece("wood_pole2", 3, -3), Piece("piece_chest_wood", -4, 0),
            Piece("portal_wood", 0, 8), Piece("wood_pole2", -12, -12), Piece("wood_pole2", 12, -12),
        ]);
        Shown(r, put, "Claude put down 7 piece(s)", "Claude: building");
        int[] ids = [.. ClaudeRun.J(put).GetProperty("placed").EnumerateArray().Select(p => p.GetProperty("id").GetInt32())];

        Shown(r, await r.T.MoveObjects([ids[0]], dx: 1), "Claude moved 1 object(s)", "Claude: moved");
        Shown(r, await r.T.SetTransform([new ClaudeTools.TransformSpec { Id = ids[1], Yaw = 45 }]), "Claude placed 1 object(s)", "Claude: placed exactly");
        Shown(r, await r.T.ReplaceObjects([ids[2]], "Birch1"), "Claude replaced 1 object(s) with Birch1", Step("replaced 1 with Birch1"));
        Shown(r, await r.T.SetContents(ids[3], [new ClaudeTools.ItemSpec { Item = "Coins", Stack = 25 }]), "Claude changed what piece_chest_wood holds", Step("filled piece_chest_wood"));
        Shown(r, await r.T.SetObjectData(ids[4], [new ClaudeTools.FieldSpec { Key = "tag", Value = "home" }]), "Claude changed portal_wood's values", Step("edited portal_wood"));
        Shown(r, await r.T.CopyObjects([ids[5]], dx: 2), "Claude made 1 copy;", "Claude: copied");
        Shown(r, await r.T.CopyObjects([ids[5]], dz: 3, copies: 2), "Claude made 2 copies;", "Claude: copied");
        Shown(r, await r.T.RemoveObjects([ids[6]]), "Claude took 1 object(s) away", Step("took 1 away"));

        // The user's own step, then Take back Claude's changes: Claude's steps, named in the language, are all found.
        s.Shape(8, 56, Formula.Compile("1", []), 3, 0, "mine");
        r.W.History.Toggle();
        Assert.True(r.W.History.TakeBackClaude.IsVisible);
        await r.W.History.TakeBackClaudes();
        var claudes = s.UndoList.Where(HistoryPanel.ByClaude).ToList();
        Assert.Equal(9, claudes.Count(c => c.RevertOf == null));
        Assert.All(claudes.Where(c => c.RevertOf == null), c => Assert.True(c.Removed, c.Label));
        Assert.Contains(s.UndoList, c => c.Label == "mine" && !c.Removed);
        Assert.False(r.W.History.TakeBackClaude.IsVisible);
    }

    // A stamp, a heightmap, nature regrown, zones marked for reset and unmarked, a blueprint and a generated dungeon:
    // each said in the language and its step named in it, behind "Claude: ".
    [AvaloniaFact]
    public async Task GroundAndWorldEditsShowInTheLanguageAndStayClaudes()
    {
        using var r = new ClaudeRun();
        r.Hut("Shed", "A small shed");
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        byte[] png = TerrainEditor.App.Heightmaps.Encode(scene.W, scene.H, scene.Heights, "test", out _, out _);
        string path = Path.Combine(Path.GetTempPath(), $"vwe-heightmap-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, png, TestContext.Current.CancellationToken);
        using var pseudo = new PseudoLang();
        try
        {
            Shown(r, await r.T.Stamp("mountain", 0, 0, radius: 15, height: 5), "Claude pressed a stamp into ", Step("stamp mountain"));
            Shown(r, await r.T.ImportHeightmap(path, -32, -32, 32, 32, edge: 0), "Claude imported a heightmap into ", Step("heightmap import"));
        }
        finally
        {
            File.Delete(path);
        }
        int[] trees = [.. ClaudeRun.J(await r.T.ListObjects(kind: "Trees", limit: 2000)).EnumerateArray().Select(t => t.GetProperty("id").GetInt32())];
        Assert.NotEmpty(trees);
        await r.T.RemoveObjects(trees);
        // West of the ground the start temple keeps clear.
        string regrew = await r.T.RegrowNature(-32, -32, -23, 0, ["trees"]);
        int grown = int.Parse(r.W.LastMessageEn.Split(' ')[2], System.Globalization.CultureInfo.InvariantCulture);
        Shown(r, regrew, "Claude regrew ", Step($"regrew {grown} object(s)"));
        Shown(r, await r.T.ResetZones(0, 0, 10, 10), "Claude marked 1 zone(s) for reset", Step("reset 1 zone(s)"));
        Shown(r, await r.T.ResetZones(0, 0, 10, 10, cancel: true), "Claude unmarked 1 zone(s).", Step("cancelled reset of 1 zone(s)"));
        Shown(r, await r.T.AddBlueprint("Shed", 15, 15), "Claude added “Shed”: 2 piece(s)", Step("added Shed"));
        string made = await r.T.GenerateDungeon(size: 1, levels: 1, seed: 3, x: 5, z: 5);
        string name = r.W.LastMessageEn["Generated ".Length..r.W.LastMessageEn.IndexOf(':', StringComparison.Ordinal)];
        Shown(r, made, "Generated ", Step("generated dungeon " + name));
    }

    // The dungeon tools: a room added and taken out, a door put in and taken out, the open ends closed. The status bar
    // says each in the language; Claude's answers stay English; the steps are Claude's, named in the language.
    [AvaloniaFact]
    public async Task DungeonEditsShowInTheLanguageAndAnswerInEnglish()
    {
        using var r = await ClaudeToolsDungeonsTests.WithCave();
        using var pseudo = new PseudoLang();
        string added = await r.T.AddDungeonRoom(await ClaudeToolsDungeonsTests.Id(r), 0, "cave_new_corridor03", contents: false, doors: false);
        Shown(r, added, "Added cave_new_corridor03.", Step("added cave_new_corridor03"));
        int id = ClaudeToolsDungeonsTests.NewId(added);
        Shown(r, await r.T.SetDungeonDoor(id, 0), "Added a door (", Step("added a dungeon door"));
        Shown(r, await r.T.SetDungeonDoor(id, 0, door: false), "Removed the door.", Step("removed a dungeon door"));
        string removed = await r.T.RemoveDungeonRoom(id, 1);
        Shown(r, removed, "Removed cave_new_corridor03", Step("removed cave_new_corridor03"));
        Shown(r, await r.T.CloseDungeonEnds(ClaudeToolsDungeonsTests.NewId(removed)), "Closed 3 open end(s)", Step("closed open ends"));
    }

    // The ready tasks, run as scripts: their steps keep "Claude: " before the task's name in the language; a script
    // Claude names itself keeps Claude's words.
    [AvaloniaFact]
    public async Task ReadyTasksAndScriptsAreClaudesStepsInTheLanguage()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var s = r.W.Session!;
        using var pseudo = new PseudoLang();
        foreach (var (answer, step) in new[]
        {
            (await r.T.Flatten(10, 10, 20, 20, edge: 0), Step("flatten")),
            (await r.T.PaintArea("dirt", 0, 0, 4, 4), Step("paint dirt")),
            (await r.T.Road([[-20f, -20f], [0f, -20f]], width: 3, paint: "none"), Step("road")),
            (await r.T.Forest(5, -30, 30, -5, ["Birch1"], spacing: 7), Step("forest")),
            (await r.T.RunScript("Ground.Raise(0, 0, 3);", "a hill"), "Claude: a hill"),
        })
        {
            Assert.DoesNotContain(PseudoLang.Open, answer);
            Assert.Contains(s.UndoList, c => c.Label == step && HistoryPanel.ByClaude(c));
        }
    }
}
