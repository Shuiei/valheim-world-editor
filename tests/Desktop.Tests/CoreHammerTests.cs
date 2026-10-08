using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

// The game's hammer (Hammer, after Player.UpdatePlacementGhost and FindClosestSnapPoints) with the
// game's own pieces: on the ground a piece rests on its colliders; pointed at a wall's top, the next
// wall stands on it; near a wall's end, beside it; turned as asked; never right where the same piece
// stands; rugs (clip everything) at the point itself.
public class CoreHammerTests
{
	private const float Ground = 30;
	private static readonly Quaternion None = Quaternion.Identity;

	private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

	private static Hammer.Hit Down(float x, float z, IEnumerable<Hammer.Placed> pieces)
	{
		var o = new Vector3(x, Ground + 50, z);
		return Hammer.Ray(o, -Vector3.UnitY, pieces, 50)!;
	}

	[Fact]
	public void OnTheGroundAPieceRestsOnItsColliders()
	{
		var none = new List<Hammer.Placed>();
		var (wall, _) = Hammer.Place("woodwall", None, Down(0, 0, none), none);
		Assert.Equal(Ground + 1, wall.Y, 3);
		var (stone, _) = Hammer.Place("stone_wall_2x1", None, Down(5, 0, none), none);
		Assert.Equal(Ground + 0.5f, stone.Y, 3);
		var (bench, _) = Hammer.Place("piece_workbench", None, Down(9, 0, none), none);
		// Its box's bottom is 0.025 m above its origin: the origin goes that much below the ground's point.
		Assert.InRange(bench.Y, Ground - 0.025f - 1e-3f, Ground - 0.025f + 1e-3f);
		// A rug clips everything: its origin at the point.
		var (rug, _) = Hammer.Place("rug_moose", None, Down(12, 0, none), none);
		Near(new Vector3(12, Ground, 0), rug);
	}

	[Fact]
	public void PointedAtAWallsTopTheNextStandsOnItAndBesideItsEnd()
	{
		var pieces = new List<Hammer.Placed> { new(0, "woodwall", new Vector3(0, Ground + 1, 0), None) };
		// The ray meets the wall's top (not the ground behind it), facing up.
		var hit = Down(0.3f, 0, pieces);
		Assert.Equal(0, hit.Piece);
		Assert.Equal(Ground + 2, hit.Point.Y, 3);
		Near(Vector3.UnitY, hit.Normal);
		var (up, to) = Hammer.Place("woodwall", None, hit, pieces);
		Near(new Vector3(0, Ground + 3, 0), up);
		Assert.Equal(0, to);
		// On the ground just past its end: beside it, end to end.
		var (beside, to2) = Hammer.Place("woodwall", None, Down(2.2f, 0, pieces), pieces);
		Near(new Vector3(2, Ground + 1, 0), beside);
		Assert.Equal(0, to2);
		// Turned a quarter: still meets it at a snap point (a corner), and stays turned.
		var quarter = BlueprintFormats.FromEuler(new Vector3(0, 90, 0));
		var (corner, to3) = Hammer.Place("woodwall", quarter, Down(1.2f, 1.1f, pieces), pieces);
		Assert.Equal(0, to3);
		Assert.Equal(1, MathF.Abs(corner.X), 3);
		// Snapping off (the game's Alt): where it touches.
		var (free, none) = Hammer.Place("woodwall", None, Down(2.2f, 0, pieces), pieces, snap: false);
		Assert.Null(none);
		Near(new Vector3(2.2f, Ground + 1, 0), free);
		// Lifted (Ctrl + wheel) before snapping: up to the next row's snap points.
		var (lifted, _) = Hammer.Place("woodwall", None, Down(2.2f, 0, pieces), pieces, lift: 2);
		Near(new Vector3(2, Ground + 3, 0), lifted);
	}

	[Fact]
	public void NotRightWhereTheSamePieceStands()
	{
		var pieces = new List<Hammer.Placed>
		{
			new(0, "woodwall", new Vector3(0, Ground + 1, 0), None),
			new(1, "woodwall", new Vector3(2, Ground + 1, 0), None),
		};
		// Between the two walls' ends the snap would put it onto the second wall: refused, it stays put.
		var hit = new Hammer.Hit(null, new Vector3(2.05f, Ground, 0.4f), Vector3.UnitY);
		var (pos, to) = Hammer.Place("woodwall", None, hit, pieces);
		Assert.True(to == null || Vector3.Distance(pos, pieces[1].Position) > 0.05f);
	}

	[Fact]
	public void ClosestPointsAndRaysOnShapes()
	{
		var p = Hammer.ClosestTriangle(new Vector3(0.2f, 1, 0.2f), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ);
		Near(new Vector3(0.2f, 0, 0.2f), p);
		Near(new Vector3(1, 0, 0), Hammer.ClosestTriangle(new Vector3(3, 0, -1), Vector3.Zero, Vector3.UnitX, Vector3.UnitZ));
		// Every piece the game builds has its placement data.
		Assert.NotNull(Hammer.Get("woodwall"));
		Assert.NotEmpty(Hammer.Get("wood_roof")!.Snaps);
		Assert.True(Hammer.Get("rug_wolf")!.ClipEverything);
	}
}
