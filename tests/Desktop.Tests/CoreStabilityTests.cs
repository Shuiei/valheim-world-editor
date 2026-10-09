using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

// The game's structural support (Stability, after WearNTear.UpdateSupport) on simple buildings: a piece
// on the ground is full, support falls with each piece up or out, a floating piece breaks, and what
// rested on a broken piece breaks after it.
public class CoreStabilityTests
{
	private const float Ground = 30;
	private static float Flat(float x, float z) => Ground;

	// A wood wall (2 × 2 m, its middle at its pivot) standing with its foot at height y.
	private static Stability.Piece Wall(float x, float y) => new("woodwall", new Vector3(x, y + 1, 0), Quaternion.Identity);

	[Fact]
	public void APieceOnTheGroundHasFullSupportAndATowerLosesSomeWithEachWall()
	{
		var tower = Enumerable.Range(0, 5).Select(i => Wall(0, Ground + 2 * i)).ToList();
		var r = Stability.Solve(tower, Flat);
		Assert.Equal(100, r.Support[0], 1);
		Assert.Equal(-1, r.Colour[0]);
		for (int i = 1; i < tower.Count; i++)
		{
			Assert.True(r.Support[i] < r.Support[i - 1], $"wall {i}: {r.Support[i]} after {r.Support[i - 1]}");
			Assert.InRange(r.Colour[i], 0, 1);
		}
		Assert.Empty(r.Falls);
	}

	[Fact]
	public void ATooTallWoodTowerBreaksAtTheTopAndStoneHoldsMore()
	{
		var tower = Enumerable.Range(0, 14).Select(i => Wall(0, Ground + 2 * i)).ToList();
		var r = Stability.Solve(tower, Flat);
		Assert.NotEmpty(r.Falls);
		// It breaks from some height up; the walls below stand.
		int first = r.Falls.Min();
		Assert.InRange(first, 4, 12);
		Assert.All(Enumerable.Range(first, tower.Count - first), i => Assert.True(r.Breaks[i]));
		Assert.All(Enumerable.Range(0, first), i => Assert.False(r.Breaks[i]));
		Assert.Equal((1000f, 100f, 1f, 0.125f), Stability.Material(1));
	}

	[Fact]
	public void AFloatingPieceBreaksAndAnOverhangLosesMoreThanATower()
	{
		var floating = Stability.Solve(new[] { Wall(0, Ground + 10) }, Flat);
		Assert.True(floating.Breaks[0]);
		// Floors out from a wall: horizontal support falls faster than vertical.
		var floors = new List<Stability.Piece> { Wall(0, Ground) };
		for (int i = 0; i < 3; i++)
		{
			floors.Add(new Stability.Piece("wood_floor", new Vector3(2f * i + 1.1f, Ground + 2, 0), Quaternion.Identity));
		}
		var r = Stability.Solve(floors, Flat);
		var up = Stability.Solve(Enumerable.Range(0, 3).Select(i => Wall(0, Ground + 2 * i)).ToList(), Flat);
		Assert.True(r.Support[2] < up.Support[2], $"second floor out {r.Support[2]}, second wall up {up.Support[2]}");
	}

	[Fact]
	public void PiecesWithoutSupportRulesAreFreeAndUnknownOnesToo()
	{
		var r = Stability.Solve(new[] { new Stability.Piece("piece_workbench", new Vector3(0, Ground, 0), Quaternion.Identity), new Stability.Piece("no_such_piece", Vector3.Zero, Quaternion.Identity) }, Flat);
		Assert.False(r.Breaks[1]);
		Assert.True(r.Free[1]);
		Assert.True(Stability.Known("piece_workbench"));
		Assert.False(Stability.Known("no_such_piece"));
	}
}
