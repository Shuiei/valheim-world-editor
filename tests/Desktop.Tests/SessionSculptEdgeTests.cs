using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Edge cases of the edit session and the brushes: the history's length limit, calls outside a stroke,
// saving during a stroke or from an area opened on its own, taking out a change that marked zones for
// reset; every falloff's profile, the ragged and turned brushes and their outlines, and erosion by rain.
[Collection("World files")]
public class SessionSculptEdgeTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	[Fact]
	public void TheHistoryKeepsTheLast200Changes()
	{
		var s = EditTests.Flat(2);
		for (int i = 0; i < EditSession.HistoryLength + 5; i++)
		{
			s.Shape(10 + i % 100, 64, Two, 1, 0, $"change {i}");
		}
		Assert.Equal(EditSession.HistoryLength, s.UndoList.Count);
		Assert.Equal("change 5", s.UndoList[0].Label);
		// Deleting (another way into the history) keeps the limit too.
		var t = EditTests.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
		for (int i = 0; i < EditSession.HistoryLength; i++)
		{
			t.Shape(20, 20, Two, 1, 0, "x");
		}
		t.Delete(new[] { 0 });
		Assert.Equal(EditSession.HistoryLength, t.UndoList.Count);
		Assert.Equal("Deleted 1", t.UndoLabel);
	}

	[Fact]
	public void OutsideAStrokeStepsAndEndsDoNothing()
	{
		var s = EditTests.Flat(2);
		Assert.False(s.Stroking);
		Assert.False(s.StrokeStep(64, 64, 0.1f));
		Assert.Equal("", s.EndStroke());
		// During a stroke: undo, removing a change and saving wait for it to end.
		s.Shape(64, 64, Two, 3, 0, "raise");
		s.BeginStroke(BrushTool.Raise, 64, 64);
		Assert.True(s.Stroking);
		Assert.False(s.Undo());
		Assert.Equal("", s.RemoveChange(s.UndoList[0]));
		Assert.Throws<InvalidOperationException>(() => s.Save());
		s.EndStroke();
		Assert.True(s.Undo());
	}

	[Fact]
	public void AnAreaOpenedFromAFolderSavesThroughItsWorld()
	{
		// An area outside any open world (only tests make one) cannot be saved.
		Assert.Contains("not part of an open world", Assert.Throws<InvalidOperationException>(() => EditTests.Flat(1).Save()).Message);
		string dir = EditTests.CopyFixture();
		try
		{
			var scene = WorldScene.Load(dir, 0, 0, 1);
			Assert.NotNull(scene.Owner);
			var s = scene.Session!;
			s.Shape(32, 32, Two, 3, 0, "raise");
			bool reset = false;
			s.ThingsReset += () => reset = true;
			var r = s.Save();
			Assert.True(r.Saved, r.Message);
			// Not read again (no zone reset, no ground discs): the objects and the history stay.
			Assert.False(reset);
			Assert.Equal(0, s.Pending.Zones);
			Assert.Single(s.UndoList);
			Assert.Equal(3, WorldSave.Load(dir).SaveNumber);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, true);
		}
	}

	[Fact]
	public async Task AppliedZoneResetsReadTheAreaAgain()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label());
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		s.Shape(32, 32, Two, 3, 0, "raise");
		world.Edits.SetReset(new ZoneReset(5, 5, true, false), true);
		bool reset = false;
		s.ThingsReset += () => reset = true;
		var o = await s.ApplyLive();
		Assert.True(o.Reloaded, o.Message);
		Assert.True(reset);
		Assert.Empty(s.UndoList);
	}

	[Fact]
	public void TakingOutAChangeUnmarksItsZoneResets()
	{
		var s = EditTests.Flat(2);
		var reset = new ZoneReset(0, 0, true, false);
		s.Commit("Area: Reset", null, Array.Empty<int>(), Array.Empty<(NewObject, bool)>(), new[] { (reset, false, true) });
		Assert.Equal(1, s.Pending.Resets);
		Assert.StartsWith("Removed", s.RemoveChange(s.UndoList[0]));
		Assert.Equal(0, s.Pending.Resets);
		s.Undo();
		Assert.Equal(1, s.Pending.Resets);
	}

	[Theory]
	[InlineData(Falloff.Smooth)]
	[InlineData(Falloff.Linear)]
	[InlineData(Falloff.Dome)]
	[InlineData(Falloff.Flat)]
	[InlineData(Falloff.Peak)]
	[InlineData(Falloff.Sharp)]
	public void EveryFalloffIsFullInTheMiddleAndNothingAtTheEdge(Falloff f)
	{
		Assert.Equal(1, Brush.Fade(f, 1), 4);
		Assert.Equal(0, Brush.Fade(f, 0), 4);
		// Never weaker nearer the middle.
		float last = 0;
		for (int i = 0; i <= 20; i++)
		{
			float v = Brush.Fade(f, i / 20f);
			Assert.True(v >= last - 1e-5f, $"{f} drops at {i / 20f}");
			last = v;
		}
	}

	[Fact]
	public void TheFalloffsDifferWhereTheyShould()
	{
		Assert.Equal(0.5f, Brush.Fade(Falloff.Linear, 0.5f), 4);
		Assert.True(Brush.Fade(Falloff.Dome, 0.5f) > 0.8f);
		Assert.Equal(1, Brush.Fade(Falloff.Flat, 0.3f), 4);
		Assert.Equal(0.125f, Brush.Fade(Falloff.Peak, 0.5f), 4);
		Assert.Equal(1, Brush.Fade(Falloff.Sharp, 0.01f), 4);
	}

	[Fact]
	public void TheRaggedBrushWavesAroundTheCircle()
	{
		var b = new Brush { Radius = 10, Shape = BrushShape.Noise };
		Assert.Equal(10 * 1.42f, b.Reach, 3);
		// Along the edge, some points are in and some out: the edge is ragged.
		var w = Enumerable.Range(0, 64).Select(i => b.Weight(9.5f * MathF.Cos(i * MathF.Tau / 64), 9.5f * MathF.Sin(i * MathF.Tau / 64), 300 + 9.5f * MathF.Cos(i * MathF.Tau / 64), 200 + 9.5f * MathF.Sin(i * MathF.Tau / 64))).ToList();
		Assert.Contains(w, v => v > 0.01f);
		Assert.Contains(w, v => v == 0);
	}

	[Fact]
	public void ATurnedSquareReachesItsCornersAndItsOutlineTurns()
	{
		var b = new Brush { Radius = 10, Shape = BrushShape.Square, Turn = 45 };
		Assert.Equal(14.2f, b.Reach, 1);
		// Turned 45°: the corner now points along x, past the radius.
		Assert.True(b.Weight(13, 0) > 0);
		Assert.Equal(0, b.Weight(9, 9));
		var outline = b.Outline(8);
		Assert.Equal(8, outline.Count);
		Assert.Contains(outline, p => MathF.Abs(MathF.Abs(p.X) - 14.14f) < 0.1f && MathF.Abs(p.Z) < 0.1f);
	}

	[Fact]
	public void RainErodesTheGround()
	{
		var s = EditTests.Flat(2);
		s.Shape(64, 64, Formula.Compile("8 * (1 - d / r)", new[] { "x", "z", "d", "r", "h" }), 12, 0, "cone");
		var before = (float[])s.Scene.Heights.Clone();
		s.Brush.ErodeWater = true;
		s.Brush.Radius = 12;
		s.Brush.Strength = 1;
		s.BeginStroke(BrushTool.Erode, 64, 64);
		for (int i = 0; i < 20; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		s.EndStroke();
		Assert.Equal("Erode", s.UndoLabel);
		Assert.Contains(Enumerable.Range(0, before.Length), p => MathF.Abs(before[p] - s.Scene.Heights[p]) > 0.01f);
		// Far from the brush, untouched.
		Assert.Equal(before[10 * s.Scene.W + 10], s.Scene.Heights[10 * s.Scene.W + 10]);
	}

	[Fact]
	public void ABrushEntirelyOutsideTheAreaDoesNothing()
	{
		var s = EditTests.Flat(2);
		s.Brush.Radius = 3;
		s.BeginStroke(BrushTool.Raise, -50, -50);
		Assert.False(s.StrokeStep(-50, -50, 0.1f));
		Assert.Equal("", s.EndStroke());
		Assert.False(s.CanUndo);
	}
}
