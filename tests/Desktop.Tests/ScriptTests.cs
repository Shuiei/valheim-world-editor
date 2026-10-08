using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Script tool: C# scripts compiled in the app and run on a snapshot of the open area, their
// changes going in as one undo step.
public class ScriptTests
{
	private static ScriptHost.Changes Run(EditSession s, string code, CancellationToken cancel = default)
	{
		var (image, pdb, errors) = ScriptHost.Compile(code);
		Assert.True(image != null, string.Join("\n", errors));
		return ScriptHost.Run(image!, pdb, ScriptHost.Take(s, p => TerrainEditor.Terrain.PrefabCatalog.NameOf(p)), cancel);
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
