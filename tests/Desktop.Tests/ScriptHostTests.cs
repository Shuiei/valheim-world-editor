using System.Numerics;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.ScriptHostRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's runs: C# scripts compiled in the app and run on a snapshot of the open area, their changes going
/// in as one undo step, their paints, their mistakes and errors, the documentation's recipes and Stop.
/// </summary>
public class ScriptHostTests
{
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
    public void TheApiNamesEveryKindOfObject()
    {
        using var stream = typeof(ScriptHost).Assembly.GetManifestResourceStream("TerrainEditor.ScriptApi.csx")!;
        string api = new StreamReader(stream).ReadToEnd();
        int at = api.IndexOf("// Kinds: ", StringComparison.Ordinal);
        string kinds = api[at..api.IndexOf("public static IEnumerable<Obj> OfKind", at, StringComparison.Ordinal)];
        Assert.All(Enum.GetNames<TerrainEditor.App.ObjectKind>(), k => Assert.Contains($" {k}", kinds));
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
}
