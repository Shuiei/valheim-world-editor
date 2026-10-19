using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's structural support (Stability, after WearNTear.UpdateSupport) on simple buildings: a piece on the ground
/// is full, support falls with each piece up or out, a floating piece breaks, and what rested on a broken piece breaks
/// after it.
/// </summary>
public class StabilityTests
{
    private const float Ground = 30;
    /// <summary>
    /// Flat ground at 30 m.
    /// </summary>
    private static float Flat(float x, float z) => Ground;

    /// <summary>
    /// A wood wall (2 × 2 m, its middle at its pivot) standing with its foot at height y.
    /// </summary>
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
        var floating = Stability.Solve([Wall(0, Ground + 10)], Flat);
        Assert.True(floating.Breaks[0]);
        // Floors out from a wall: horizontal support falls faster than vertical.
        var floors = new List<Stability.Piece> { Wall(0, Ground) };
        for (int i = 0; i < 3; i++)
        {
            floors.Add(new Stability.Piece("wood_floor", new Vector3(2f * i + 1.1f, Ground + 2, 0), Quaternion.Identity));
        }
        var r = Stability.Solve(floors, Flat);
        var up = Stability.Solve([.. Enumerable.Range(0, 3).Select(i => Wall(0, Ground + 2 * i))], Flat);
        Assert.True(r.Support[2] < up.Support[2], $"second floor out {r.Support[2]}, second wall up {up.Support[2]}");
    }

    [Fact]
    public void PiecesWithoutSupportRulesAreFreeAndUnknownOnesToo()
    {
        var r = Stability.Solve([new Stability.Piece("piece_workbench", new Vector3(0, Ground, 0), Quaternion.Identity), new Stability.Piece("no_such_piece", Vector3.Zero, Quaternion.Identity)], Flat);
        Assert.False(r.Breaks[1]);
        Assert.True(r.Free[1]);
        Assert.True(Stability.Known("piece_workbench"));
        Assert.False(Stability.Known("no_such_piece"));
    }

    [Fact]
    public void ABlackMarbleSlabNeverFallsAndHoldsWhatStandsOnIt()
    {
        // Far above any ground: an 8 × 8 m slab (the game never checks its support), a stone wall on it,
        // and the same wall alone beside it.
        float sky = Ground + 5000;
        var pieces = new List<Stability.Piece>
        {
            new("blackmarble_floor_large", new Vector3(0, sky - 1, 0), Quaternion.Identity),
            new("stone_wall_4x2", new Vector3(0, sky + 1, 0), Quaternion.Identity),
            new("stone_wall_4x2", new Vector3(30, sky + 1, 0), Quaternion.Identity),
        };
        var r = Stability.Solve(pieces, Flat);
        Assert.Equal(-1, r.Colour[0]);
        Assert.Equal(1500, r.Support[0], 1);
        Assert.DoesNotContain(0, r.Falls);
        Assert.DoesNotContain(1, r.Falls);
        Assert.Equal(1000, r.Support[1], 1);
        Assert.Contains(2, r.Falls);
    }

    [Fact]
    public void APoleAgainstADoorsFrameHoldsAndAgainstItsLeafFalls()
    {
        // A wood door standing on the ground (its threshold at the ground), and two 2 m poles off the ground beside it:
        // one against its post, one against the face of its leaf. The leaf swings on a Rigidbody, which the game's
        // support check leaves out: it holds nothing.
        var pieces = new List<Stability.Piece>
        {
            new("wood_door", new Vector3(0, Ground + 1.0091f, 0), Quaternion.Identity),
            new("wood_pole2", new Vector3(1.3f, Ground + 1.5f, 0), Quaternion.Identity),
            new("wood_pole2", new Vector3(0, Ground + 1.5f, 0.35f), Quaternion.Identity),
        };
        var r = Stability.Solve(pieces, Flat);
        Assert.Equal(100, r.Support[0], 1);
        Assert.DoesNotContain(1, r.Falls);
        Assert.InRange(r.Support[1], 10, 100);
        Assert.Equal(2, Assert.Single(r.Falls));
    }

    [Fact]
    public void EveryMaterialHasTheGamesSupport()
    {
        // Ancient and ice, the strongest and a stone-like one; an unknown material gives no support at all.
        Assert.Equal((5000f, 100f, 0.25f, 1f / 15f), Stability.Material(6));
        Assert.Equal((1000f, 100f, 1f / 3f, 0.125f), Stability.Material(7));
        Assert.Equal((0f, 0f, 0f, 0f), Stability.Material(99));
    }

    [Fact]
    public void AScaledPiecesCentreOfMassIsScaledToo()
    {
        // The colliders were scaled but the centre of mass was not: a grausten wall twice the size had its centre at
        // 1 m, not 2, which threw its support distances off.
        var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);

        Assert.Equal(new Vector3(5, 2, 0), Stability.CentreOfMass(new("Piece_grausten_wall_1x2", new Vector3(5, 0, 0), Quaternion.Identity, 2)));
        var lying = Stability.CentreOfMass(new("Piece_grausten_wall_1x2", Vector3.Zero, turned, 2))!.Value;
        Assert.Equal(-2, lying.X, 0.001f);
        Assert.Equal(new Vector3(0, 1, 0), Stability.CentreOfMass(new("Piece_grausten_wall_1x2", Vector3.Zero, Quaternion.Identity, 0)));
    }
}
