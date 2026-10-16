using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool: C# scripts compiled in the app and run on a snapshot of the open area, their changes going in as
/// one undo step; and its helpers for moving objects (Move) and snapping new pieces (SnapPieces).
/// </summary>
public class ScriptHostTests
{
    /// <summary>
    /// Stopped by the token given, else with the test (when the run is cancelled).
    /// </summary>
    private static ScriptHost.Changes Run(EditSession s, string code, CancellationToken? cancel = null)
    {
        var (image, pdb, errors) = ScriptHost.Compile(code);
        Assert.True(image != null, string.Join("\n", errors));
        return ScriptHost.Run(image!, pdb, ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf), cancel ?? TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The ground height at world point (x, z) of a 1 × 1 zone area.
    /// </summary>
    private static float At(EditSession s, float x, float z) => s.Ground.HeightOf((int)(z + 32) * s.Ground.W + (int)(x + 32));

    [Fact]
    public void EveryExampleCompilesAndRuns()
    {
        foreach (var e in ScriptExamples.All)
        {
            var s = TestScenes.Flat(3);
            var ch = Run(s, e.Code);
            Assert.False(ch.Empty, $"{e.Name} changed nothing");
            ScriptHost.Apply(s, ch, e.Name);
        }
    }

    [Fact]
    public void AScriptMayChangeTheGroundFromSeveralThreads()
    {
        var s = TestScenes.Flat(2);
        var ch = Run(s, """
			System.Threading.Tasks.Parallel.For(0, 100, i =>
			{
			    for (int j = 0; j < 100; j++)
			    {
			        Ground.Set(Area.MinX + 2 + i, Area.MinZ + 2 + j, 40);
			        Ground.Paint(Area.MinX + 2 + i, Area.MinZ + 2 + j, "dirt");
			    }
			    Print("row");
			});
			""");
        Assert.Equal(10000, ch.Heights.Count);
        Assert.Equal(10000, ch.Paint.Count);
        Assert.Equal(100, ch.Output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void ANonNumberIsNoPoint()
    {
        var ch = Run(TestScenes.Flat(2), """
			Ground.Set(float.NaN, 10, 40);
			Ground.Set(10, float.PositiveInfinity, 40);
			Ground.Paint(float.NaN, float.NaN, "dirt");
			Print(Ground.Biome(float.NaN, 10));
			""");
        Assert.Empty(ch.Heights);
        Assert.Empty(ch.Paint);
        Assert.Equal("None", ch.Output.ToString().Trim());
    }

    [Fact]
    public void AScriptsChangesAreOneUndoStepAndGoPastTheLimit()
    {
        var s = TestScenes.Flat(2);
        var ch = Run(s, """
			for (float z = 40; z <= 50; z++)
			    for (float x = 40; x <= 50; x++)
			        Ground.Raise(x, z, 25);
			Print($"top {Ground.Height(45, 45):0}");
			""");
        Assert.Equal("top 55", ch.Output.ToString().Trim());
        Assert.Equal(121, ch.Heights.Count);
        Assert.Equal(0, ScriptHost.Apply(s, ch, "Script: test"));
        Assert.Equal(55, At(s, 45, 45), 3);
        // Past the ±8 m: the rest is lift, saved as ground discs.
        Assert.True(s.Ground.Lift[(45 + 32) * s.Ground.W + 45 + 32] > 16);
        Assert.Equal("Script: test", s.UndoLabel);
        Assert.True(s.Undo());
        Assert.Equal(30, At(s, 45, 45), 3);
    }

    // Object numbers that are not the area's (a script's own, out of range): left out, and the ground
    // change is still one undo step (it threw after changing the ground, with no step to undo it).
    [Fact]
    public void BadObjectNumbersAreLeftOut()
    {
        var s = TestScenes.Flat(2);
        var ch = new ScriptHost.Changes();
        ch.Heights[45 * s.Ground.W + 45] = 33;
        ch.Remove.Add(-1);
        ch.Remove.Add(1_000_000);
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.Equal("Script: test", s.UndoLabel);
        Assert.True(s.Undo());
        Assert.Equal(30, s.Ground.HeightOf(45 * s.Ground.W + 45), 3);
    }

    // What tells a script's run that the area changed while it ran (then nothing is applied).
    [Fact]
    public void EveryStepMovesTheGeneration()
    {
        var s = TestScenes.Flat(2);
        int g0 = s.Generation;
        s.Shape(40, 40, Formula.Compile("2", []), 4, 0, "x");
        int g1 = s.Generation;
        Assert.NotEqual(g0, g1);
        s.Undo();
        Assert.NotEqual(g1, s.Generation);
    }

    [Fact]
    public void WithoutNoLimitTheGameLimitHolds()
    {
        var s = TestScenes.Flat(2);
        var ch = Run(s, "Ground.NoLimit = false; Ground.Raise(45, 45, 20);");
        Assert.Equal(1, ScriptHost.Apply(s, ch, "Script: test"));
        Assert.Equal(38, At(s, 45, 45), 3);
    }

    // Changes that fail part-way (a height for a point outside the area) leave the area's No limit as it was: it
    // stayed as the script set it, and every later brush went past the game's ±8 m.
    [Fact]
    public void FailedChangesLeaveNoLimitAsItWas()
    {
        var s = TestScenes.Flat(2);
        var ch = new ScriptHost.Changes();
        ch.Heights[-1] = 5;
        Assert.Throws<IndexOutOfRangeException>(() => ScriptHost.Apply(s, ch, "Script: test"));
        Assert.False(s.Ground.NoLimit);
    }

    [Fact]
    public void MistakesAndErrorsSayWhichLine()
    {
        var (image, _, errors) = ScriptHost.Compile("int a = 1;\nGround.Raise(1, 2);\n");
        Assert.Null(image);
        Assert.StartsWith("line 2: ", Assert.Single(errors));
        var s = TestScenes.Flat(1);
        var ex = Assert.Throws<ArgumentException>(() => Run(s, "Print(1);\nObjects.Place(\"NoSuchThing\", 10, 10);\n"));
        Assert.Contains("NoSuchThing", ex.Message);
        Assert.Equal("line 2: ", ScriptHost.Where(ex));
    }

    [Fact]
    public void ObjectsArePlacedOnTheNewGroundAndRemoved()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 0, false);
        var s = TestScenes.Flat(2, tree);
        var ch = Run(s, """
			var t = Objects.OfKind("Trees").Single();
			Objects.Remove(t);
			Ground.Raise(20, 20, 3);
			Objects.Place("rock4_forest", 20, 20, yaw: 90, scale: 0.5f);
			Print(Objects.CanPlace("Beech1"));
			""");
        Assert.Equal("True", ch.Output.ToString().Trim());
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.True(s.Scene.Things[0].Gone);
        var rock = s.Scene.Things[^1];
        Assert.Equal(StableHash.Of("rock4_forest"), rock.Prefab);
        Assert.Equal(33, rock.Position.Y, 2);
        Assert.Equal(0.5f, rock.Scale);
    }

    [Fact]
    public void PaintAndMountainsWork()
    {
        var s = TestScenes.Flat(5);
        var ch = Run(s, """
			Ground.Paint(10, 10, "paved");
			Ground.Mountain(Area.CenterX, Area.CenterZ, "Mesa", height: 30, radius: 60, seed: 4);
			""");
        ScriptHost.Apply(s, ch, "Script: test");
        int p = (10 + 32) * s.Ground.W + 10 + 32;
        Assert.Equal(1, s.Ground.PMod[p]);
        Assert.Equal(1, s.Ground.Paint[p * 4 + 2], 3);
        float cx = s.Scene.Cx, cz = s.Scene.Cz;
        Assert.Equal(60, At(s, cx, cz), 1);
    }

    // docs/scripting.md's recipes: each one compiles and runs, so the documentation stays true.
    [Fact]
    public void TheDocumentationsRecipesRun()
    {
        string doc = File.ReadAllText(Path.Combine(Fixtures.Root, "..", "..", "docs", "scripting.md"));
        var recipes = System.Text.RegularExpressions.Regex.Matches(doc, "```csharp\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value).ToList();
        Assert.True(recipes.Count >= 6, $"{recipes.Count} recipes");
        foreach (string code in recipes)
        {
            var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 0, false);
            var bush = new WorldScene.Thing(6, StableHash.Of("RaspberryBush"), new Vector3(20, 30, 10), Vector3.Zero, 0, false);
            var rock = new WorldScene.Thing(7, StableHash.Of("rock4_forest"), new Vector3(30, 30, 10), new Vector3(15, 30, 0), 0, false);
            var s = TestScenes.Flat(3, tree, bush, rock);
            // Dry land, as most of a world is (the flat test ground is at sea level).
            Array.Fill(s.Ground.Base, 45f);
            Array.Fill(s.Scene.Heights, 45f);
            var ch = Run(s, code);
            Assert.False(ch.Empty, "a recipe changed nothing:\n" + code);
            ScriptHost.Apply(s, ch, "recipe");
        }
    }

    [Fact]
    public void AStoppedScriptChangesNothing()
    {
        var s = TestScenes.Flat(2);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Run(s, "foreach (var (x, z) in Area.Points()) Ground.Raise(x, z, 1);", cancel.Token));
        Assert.Equal(30, At(s, 10, 10), 3);
    }

    // A loop of the script's own that calls nothing of the API's is still ended by Stop and the 2-minute limit.
    [Theory]
    [InlineData("while (true) { }")]
    [InlineData("int n = 0;\nfor (;;) n++;")]
    [InlineData("do { } while (true);")]
    [InlineData("foreach (int i in Enumerable.Repeat(0, int.MaxValue)) { }")]
    [InlineData("again: goto again;")]
    [InlineData("static IEnumerable<int> Forever() { while (true) yield return 0; }\nforeach (int i in Forever()) { }")]
    public async Task AScriptsOwnEndlessLoopIsStopped(string code)
    {
        var s = TestScenes.Flat(1);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var run = Task.Run(() => Run(s, code, cancel.Token), TestContext.Current.CancellationToken);
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    // The checks put into loops hold no line break: an error after a loop still says the script's own line.
    [Fact]
    public void LoopsKeepTheScriptsLineNumbers()
    {
        var s = TestScenes.Flat(1);
        var ex = Assert.Throws<ArgumentException>(() => Run(s, "for (int i = 0; i < 2; i++)\n    Print(i);\nwhile (false)\n{\n}\nObjects.Place(\"NoSuchThing\", 10, 10);\n"));
        Assert.Equal("line 6: ", ScriptHost.Where(ex));
        var (image, _, errors) = ScriptHost.Compile("for (int i = 0; i < 2; i++)\n{\n}\nGround.Raise(1, 2);\n");
        Assert.Null(image);
        Assert.StartsWith("line 4: ", Assert.Single(errors));
    }

    // The checks put into loops keep #if and the other directives at the start of their own lines: next to a loop's
    // body, before it or after it, the script still compiles.
    [Theory]
    [InlineData("while (true)\n#if true\n    break;\n#endif\nPrint(1);\n")]
    [InlineData("while (false) { }\n#if true\nPrint(1);\n#endif\n")]
    [InlineData("int n = 0;\nagain: n++;\n#if true\nif (n < 3) goto again;\n#endif\nPrint(n);\n")]
    public void LoopsNextToDirectivesStillCompile(string code) => Assert.Empty(ScriptHost.Compile(code).Errors);

    // A step that cannot move along the area would give the same point for ever: it is refused.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e-7f")]
    [InlineData("float.NaN")]
    public void APointsStepThatCannotMoveIsRefused(string step)
    {
        var s = TestScenes.Flat(1);
        var ex = Assert.Throws<ArgumentException>(() => Run(s, $"Print(Area.Points({step}).Count());"));
        Assert.Contains("give one above 0", ex.Message);
    }

    // Counted steps give the points a step that adds up exactly always gave.
    [Fact]
    public void PointsAreCountedInSteps()
    {
        var s = TestScenes.Flat(1);
        Assert.Equal("16", Run(s, "Print(Area.Points(16).Count());").Output.ToString().Trim());
        Assert.Equal("15876", Run(s, "Print(Area.Points(0.5f).Count());").Output.ToString().Trim());
    }

    // A shape or mountain far larger than the area ends, having changed only the area (a float counter past 2^24 m
    // stopped growing and never ended).
    [Theory]
    [InlineData("Ground.Shape(0, 0, 1e9f, (dx, dz) => 1);")]
    [InlineData("Ground.Mountain(0, 0, height: 10, radius: 1e9f);")]
    public async Task AHugeShapeEnds(string code)
    {
        var s = TestScenes.Flat(1);
        var run = Task.Run(() => Run(s, code), TestContext.Current.CancellationToken);
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)));
        Assert.NotEmpty((await run).Heights);
    }

    // The whole-metre offsets within reach that can land in a span: short however large the reach, kept up to 2^24 m,
    // none when there is nothing to count.
    [Fact]
    public void OffsetsAreCountedOnlyWhereTheyCanLand()
    {
        Assert.Equal((-4, 3), ScriptHost.Offsets(0, 3.5f, -100, 100));
        Assert.Equal((-11, 10), ScriptHost.Offsets(0.5f, 1e9f, -10, 10));
        Assert.Equal((-11, 10), ScriptHost.Offsets(0.5f, float.PositiveInfinity, -10, 10));
        Assert.Equal((-5, 2), ScriptHost.Offsets(0, 5, -100, 2));
        // Far off: the offsets that land and can be counted (up to 2^24 m) are kept.
        Assert.Equal((-16777216, -16777154), ScriptHost.Offsets(16777218f, 16777218f, 0, 64));
        foreach (var (c, reach) in new[] { (0f, -1f), (0f, float.NaN), (float.NaN, 5f), (1e9f, 5f), (-1e30f, 1e31f) })
        {
            var (from, to) = ScriptHost.Offsets(c, reach, -10, 10);
            Assert.True(from > to, $"{c}, {reach}: {from}..{to}");
        }
    }

    /// <summary>A wooden wall standing at (20, 31, 20), its front north, as thing 0 of a flat 1-zone area.</summary>
    private static EditSession Wall() =>
        TestScenes.Flat(1, new WorldScene.Thing(7, StableHash.Of("woodwall"), new Vector3(20, 31, 20), Vector3.Zero, 0, true));

    // Objects as scripts see them carry their turn and size; a new one is put down tilted; one there is moved, turned
    // and sized in the same undo step as the rest.
    [Fact]
    public void ObjectsAreSeenTurnedAndAreMovedInTheSameStep()
    {
        var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), new Vector3(0, 45, 0), 1.5f, false);
        var s = TestScenes.Flat(2, tree);
        var ch = Run(s, """
			var t = Objects.All.Single();
			Print($"{t.Yaw:0} {t.Scale * 10:0}");
			Objects.Move(t, x: 12, yaw: 90, scale: 2);
			Objects.Place("rock4_forest", 20, 20, yaw: 10, pitch: 5, roll: -5);
			Ground.Raise(30, 30, 1);
			""");
        Assert.Equal("45 15", ch.Output.ToString().Trim());
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.Single(s.UndoList);
        Assert.True(s.Scene.Things[0].Gone);
        var rock = s.Scene.Things.Single(t => !t.Gone && t.Prefab == StableHash.Of("rock4_forest"));
        Assert.Equal(new Vector3(5, 10, -5), rock.Rotation);
        var moved = s.Scene.Things.Single(t => !t.Gone && t.Prefab == StableHash.Of("Beech1"));
        Assert.Equal((new Vector3(12, 30, 10), new Vector3(0, 90, 0), 2f), (moved.Position, moved.Rotation, moved.Scale));
    }

    // Move: what is left out stays (as it was, or as an earlier Move left it); an object not in the area, or a size
    // not above 0, is refused.
    [Fact]
    public void AMoveKeepsWhatIsLeftOut()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        ScriptHost.Move(snap, ch, 0, 25, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
        ScriptHost.Move(snap, ch, 0, float.NaN, float.NaN, 33, 90, float.NaN, float.NaN, 2);
        Assert.Equal((new Vector3(25, 33, 20), new Vector3(0, 90, 0), 2f), ch.Moves[0]);
        Assert.Throws<ArgumentException>(() => ScriptHost.Move(snap, ch, 9, 0, 0, 0, 0, 0, 0, 1));
        Assert.Throws<ArgumentException>(() => ScriptHost.Move(snap, ch, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    // Place and Move took NaN or infinite places and turns, negative sizes and points outside the area, and wrote them
    // into the world. They are refused; NaN still means "on the ground" for Place's y and "as it was" for Move.
    [Fact]
    public void NonNumbersSizesBelowZeroAndPlacesOutsideTheAreaAreRefused()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        void Place(float x, float z, float yaw = 0, float scale = 0, float y = float.NaN, float pitch = 0, float roll = 0) =>
            ScriptHost.Place(snap, ch, "rock4_forest", x, z, yaw, scale, y, pitch, roll, false);
        void Move(float x = float.NaN, float z = float.NaN, float y = float.NaN, float yaw = float.NaN, float pitch = float.NaN, float roll = float.NaN) =>
            ScriptHost.Move(snap, ch, 0, x, z, y, yaw, pitch, roll, float.NaN);
        Assert.Throws<ArgumentException>(() => Place(float.NaN, 0));
        Assert.Throws<ArgumentException>(() => Place(0, float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, y: float.NegativeInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, yaw: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(0, 0, pitch: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Place(0, 0, roll: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(0, 0, scale: -1));
        Assert.Throws<ArgumentException>(() => Place(0, 0, scale: float.NaN));
        Assert.Throws<ArgumentException>(() => Place(32.5f, 0));
        Assert.Throws<ArgumentException>(() => Place(0, -40));
        Assert.Throws<ArgumentException>(() => Move(x: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Move(y: float.NegativeInfinity));
        Assert.Throws<ArgumentException>(() => Move(yaw: float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => Move(z: 100));
        Assert.Empty(ch.Add);
        Assert.Empty(ch.Moves);
        // The area's edges count; NaN y is the ground's, 0 the prefab's size.
        Place(-32, 32);
        Place(32, -32, scale: 2, y: 40);
        Move(x: -32, yaw: 90);
        Assert.Equal(2, ch.Add.Count);
        Assert.True(float.IsNaN(ch.Add[0].Position.Y));
        Assert.Equal(new Vector3(-32, 31, 20), ch.Moves[0].Position);
    }

    // A piece placed inside the area near its edge, snapped 0.4 m to a wall standing beyond the edge, would end outside
    // it: refused.
    [Fact]
    public void APieceSnappedOutsideTheAreaIsRefused()
    {
        var s = TestScenes.Flat(1, new WorldScene.Thing(7, StableHash.Of("woodwall"), new Vector3(34.2f, 31, 20), Vector3.Zero, 0, true));
        var snap = ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf);
        var ch = new ScriptHost.Changes();
        ch.Add.Add((StableHash.Of("woodwall"), "woodwall", new Vector3(31.8f, 31, 20), Vector3.Zero, 0, true));
        var ex = Assert.Throws<ArgumentException>(() => ScriptHost.SnapPieces(snap, ch, TestContext.Current.CancellationToken));
        Assert.Contains("snaps to", ex.Message);
        Assert.Equal(31.8f, ch.Add[0].Position.X);
    }

    // SnapPieces: a wall placed with snap 0.3 m off meets the one standing; without snap it stays; a wall the script
    // took away is not snapped to; Stop ends the snapping.
    [Fact]
    public void NewPiecesSnapToThePiecesStanding()
    {
        var snap = ScriptHost.Take(Wall(), Terrain.PrefabCatalog.NameOf);
        int wall = StableHash.Of("woodwall");
        var ch = new ScriptHost.Changes();
        ch.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        ch.Add.Add((wall, "woodwall", new Vector3(17.7f, 31, 20), Vector3.Zero, 0, false));
        ScriptHost.SnapPieces(snap, ch, TestContext.Current.CancellationToken);
        Assert.Equal(22, ch.Add[0].Position.X, 3);
        Assert.Equal(17.7f, ch.Add[1].Position.X, 3);

        var gone = new ScriptHost.Changes();
        gone.Remove.Add(0);
        gone.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        ScriptHost.SnapPieces(snap, gone, TestContext.Current.CancellationToken);
        Assert.Equal(22.3f, gone.Add[0].Position.X, 3);

        var stopped = new ScriptHost.Changes();
        stopped.Add.Add((wall, "woodwall", new Vector3(22.3f, 31, 20), Vector3.Zero, 0, true));
        Assert.Throws<OperationCanceledException>(() => ScriptHost.SnapPieces(snap, stopped, new CancellationToken(canceled: true)));
        Assert.Equal(22.3f, stopped.Add[0].Position.X, 3);
    }
}
