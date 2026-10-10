using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Script tool: C# scripts compiled in the app and run on a snapshot of the open area, their
// changes going in as one undo step.
public class ScriptTests
{
	// Stopped by the token given, else with the test (when the run is cancelled).
	private static ScriptHost.Changes Run(EditSession s, string code, CancellationToken? cancel = null)
	{
		var (image, pdb, errors) = ScriptHost.Compile(code);
		Assert.True(image != null, string.Join("\n", errors));
		return ScriptHost.Run(image!, pdb, ScriptHost.Take(s, p => TerrainEditor.Terrain.PrefabCatalog.NameOf(p)), cancel ?? TestContext.Current.CancellationToken);
	}

	private static float At(EditSession s, float x, float z) => s.Ground.HeightOf((int)(z + 32) * s.Ground.W + (int)(x + 32));

	[Fact]
	public void EveryExampleCompilesAndRuns()
	{
		foreach (var e in ScriptExamples.All)
		{
			var s = EditTests.Flat(3);
			var ch = Run(s, e.Code);
			Assert.False(ch.Empty, $"{e.Name} changed nothing");
			ScriptHost.Apply(s, ch, e.Name);
		}
	}

	[Fact]
	public void AScriptsChangesAreOneUndoStepAndGoPastTheLimit()
	{
		var s = EditTests.Flat(2);
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
		var s = EditTests.Flat(2);
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
		var s = EditTests.Flat(2);
		int g0 = s.Generation;
		s.Shape(40, 40, Formula.Compile("2", new string[0]), 4, 0, "x");
		int g1 = s.Generation;
		Assert.NotEqual(g0, g1);
		s.Undo();
		Assert.NotEqual(g1, s.Generation);
	}

	[Fact]
	public void WithoutNoLimitTheGameLimitHolds()
	{
		var s = EditTests.Flat(2);
		var ch = Run(s, "Ground.NoLimit = false; Ground.Raise(45, 45, 20);");
		Assert.Equal(1, ScriptHost.Apply(s, ch, "Script: test"));
		Assert.Equal(38, At(s, 45, 45), 3);
	}

	[Fact]
	public void MistakesAndErrorsSayWhichLine()
	{
		var (image, _, errors) = ScriptHost.Compile("int a = 1;\nGround.Raise(1, 2);\n");
		Assert.Null(image);
		Assert.StartsWith("line 2: ", Assert.Single(errors));
		var s = EditTests.Flat(1);
		var ex = Assert.Throws<ArgumentException>(() => Run(s, "Print(1);\nObjects.Place(\"NoSuchThing\", 10, 10);\n"));
		Assert.Contains("NoSuchThing", ex.Message);
		Assert.Equal("line 2: ", ScriptHost.Where(ex));
	}

	[Fact]
	public void ObjectsArePlacedOnTheNewGroundAndRemoved()
	{
		var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 0, false);
		var s = EditTests.Flat(2, tree);
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
		var s = EditTests.Flat(5);
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
		string doc = File.ReadAllText(Path.Combine(EditorProcess.Fixtures(), "..", "..", "docs", "scripting.md"));
		var recipes = System.Text.RegularExpressions.Regex.Matches(doc, "```csharp\n(.*?)```", System.Text.RegularExpressions.RegexOptions.Singleline)
			.Select(m => m.Groups[1].Value).ToList();
		Assert.True(recipes.Count >= 6, $"{recipes.Count} recipes");
		foreach (string code in recipes)
		{
			var tree = new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(10, 30, 10), Vector3.Zero, 0, false);
			var bush = new WorldScene.Thing(6, StableHash.Of("RaspberryBush"), new Vector3(20, 30, 10), Vector3.Zero, 0, false);
			var s = EditTests.Flat(3, tree, bush);
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
		var s = EditTests.Flat(2);
		using var cancel = new CancellationTokenSource();
		cancel.Cancel();
		Assert.ThrowsAny<OperationCanceledException>(() => Run(s, "foreach (var (x, z) in Area.Points()) Ground.Raise(x, z, 1);", cancel.Token));
		Assert.Equal(30, At(s, 10, 10), 3);
	}
}
