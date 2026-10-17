using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's hammer (Hammer, after Player.UpdatePlacementGhost and FindClosestSnapPoints) with the game's own pieces:
/// on the ground a piece rests on its colliders; pointed at a wall's top, the next wall stands on it; near a wall's
/// end, beside it; turned as asked; never right where the same piece stands; rugs (clip everything) at the point
/// itself.
/// </summary>
public class HammerTests
{
    private const float Ground = 30;
    private static readonly Quaternion None = Quaternion.Identity;

    /// <summary>
    /// Asserts two points are within a millimetre.
    /// </summary>
    private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    /// <summary>
    /// The hammer's hit looking straight down at x, z from 50 m above the ground.
    /// </summary>
    private static Hammer.Hit Down(float x, float z, IEnumerable<Hammer.Placed> pieces)
    {
        var o = new Vector3(x, Ground + 50, z);
        return Hammer.Ray(o, -Vector3.UnitY, pieces, 50)!;
    }

    [Fact]
    public void TheLongestReachIsOfThePieceThatReachesFurthest()
    {
        float most = Hammer.MaxRadius();
        Assert.True(most > 0);
        Assert.All(["woodwall", "wood_floor", "piece_workbench"], p => Assert.True(Hammer.Get(p)!.Radius <= most, p));
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

    [Fact]
    public void TheCutHidesWhatIsAboveItFromTheBuildRay()
    {
        var pieces = new List<Hammer.Placed> { new(0, "woodwall", new Vector3(0, Ground + 1, 0), None) };
        // Cut at 1.5 m above the ground: the wall's top (2 m) is not pointed at; the ray goes on.
        var hit = Hammer.Ray(new Vector3(0.3f, Ground + 50, 0), -Vector3.UnitY, pieces, 50, Ground + 1.5f)!;
        Assert.Null(hit.Piece);
        Assert.Equal(Ground, hit.Point.Y, 3);
        Assert.Equal(0, Hammer.Ray(new Vector3(0.3f, Ground + 50, 0), -Vector3.UnitY, pieces, 50)!.Piece);
    }

    /// <summary>
    /// A collider of a kind the hammer does not know.
    /// </summary>
    private sealed record Odd : Hammer.Shape;

    [Fact]
    public void MeshesCapsulesAndSpheresArePointedAtAndRestOnTheGround()
    {
        var none = new List<Hammer.Placed>();
        // The chest's collider is a convex mesh: on the ground its lowest vertex touches the ground's point.
        var (chest, _) = Hammer.Place("piece_chest_wood", None, Down(0, 0, none), none);
        Assert.Equal(Ground - Hammer.Bottom("piece_chest_wood", None), chest.Y, 2);
        // The maypole's capsules and spheres: it rests on its lowest capsule's end.
        var (pole, _) = Hammer.Place("piece_maypole", None, Down(10, 0, none), none);
        Assert.Equal(Ground - Hammer.Bottom("piece_maypole", None), pole.Y, 2);
        // A sapling (a capsule) clips into the ground: its origin at the point.
        var (sapling, _) = Hammer.Place("Oak_Sapling", None, Down(20, 0, none), none);
        Near(new Vector3(20, Ground, 0), sapling);
        // Pointed at from above, the ray stops on the chest's mesh and on the maypole's sphere.
        var pieces = new List<Hammer.Placed> { new(0, "piece_chest_wood", chest, None), new(1, "piece_maypole", pole, None) };
        var onChest = Down(0, 0, pieces);
        Assert.Equal(0, onChest.Piece);
        Assert.True(onChest.Point.Y > Ground + 0.3f);
        Assert.True(onChest.Normal.Y > 0.5f);
        var onSphere = Down(10 - 0.35f, -1.486f, pieces);
        Assert.Equal(1, onSphere.Piece);
        Assert.Equal(pole.Y + 3.005f + 0.555f, onSphere.Point.Y, 2);
    }

    [Fact]
    public void OutlinesHoldEveryColliderKindAndNothingForAnUnknownPiece()
    {
        // The maypole's four capsules and two spheres: six points round each end and centre.
        Assert.Equal(4 * 12 + 2 * 6, Hammer.Outline("piece_maypole", None).Count());
        var chest = Hammer.Get("piece_chest_wood")!;
        Assert.Equal(((Hammer.Mesh)chest.Colliders[0]).V.Length, Hammer.Outline("piece_chest_wood", None).Count());
        // Scaled and turned: the lowest point scales with it.
        Assert.Equal(2 * Hammer.Bottom("piece_maypole", None), Hammer.Bottom("piece_maypole", None, 2), 3);
        Assert.Empty(Hammer.Outline("no_such_piece", None));
        Assert.Equal(0, Hammer.Bottom("no_such_piece", None));
        Assert.Null(Hammer.Get("no_such_piece"));
    }

    [Fact]
    public void ARadiusReachesTheFarthestColliderOfEachKind()
    {
        Hammer.Shape[] shapes =
        [
            new Hammer.Sphere(new Vector3(3, 0, 0), 1),
            new Hammer.Capsule(new Vector3(0, 1, 0), new Vector3(0, 5, 0), 0.5f),
            new Hammer.Mesh(true, [new Vector3(0, 0, 7)], [0, 0, 0]),
            new Odd(),
        ];
        Assert.Equal(7, new Hammer.Data(false, false, false, false, [], shapes).Radius, 3);
        Assert.Equal(5.5f, new Hammer.Data(false, false, false, false, [], shapes[..2]).Radius, 3);
        Assert.Equal(0, new Hammer.Data(false, false, false, false, [], [new Odd()]).Radius);
    }

    // A collider of a kind the scan does not write (another version's, or a letter's case changed) is left out; the
    // piece keeps its other colliders.
    [Theory]
    [InlineData("x")]
    [InlineData("B")]
    public void AColliderOfAnUnknownKindIsLeftOut(string kind)
    {
        var read = Hammer.Read($$$"""{"p":{"f":{"g":1},"s":[[0,1,0]],"c":[["{{{kind}}}",9,9,9],["s",0,1,0,0.5]]}}""");

        var p = read["p"];
        var sphere = Assert.IsType<Hammer.Sphere>(Assert.Single(p.Colliders));
        Assert.Equal((new Vector3(0, 1, 0), 0.5f), (sphere.C, sphere.R));
        Assert.True(p.Ground);
    }

    [Fact]
    public void ClosestPointsOnSpheresCapsulesAndConvexMeshes()
    {
        var sphere = new Hammer.Sphere(new Vector3(1, 0, 0), 1);
        Near(new Vector3(1.5f, 0, 0), Hammer.Closest(sphere, new Vector3(1.5f, 0, 0)));
        Near(new Vector3(2, 0, 0), Hammer.Closest(sphere, new Vector3(5, 0, 0)));
        var capsule = new Hammer.Capsule(Vector3.Zero, new Vector3(0, 2, 0), 0.5f);
        Near(new Vector3(0, 1, 0.2f), Hammer.Closest(capsule, new Vector3(0, 1, 0.2f)));
        Near(new Vector3(0.5f, 1, 0), Hammer.Closest(capsule, new Vector3(3, 1, 0)));
        // Past an end: on the end's half sphere.
        Near(new Vector3(0, 2.5f, 0), Hammer.Closest(capsule, new Vector3(0, 9, 0)));
        // A capsule with no length is a sphere.
        Near(new Vector3(0, 0, 0.5f), Hammer.Closest(capsule with { B = Vector3.Zero }, new Vector3(0, 0, 4)));
        var cube = Cube();
        Near(new Vector3(0.2f, 0.3f, -0.1f), Hammer.Closest(cube, new Vector3(0.2f, 0.3f, -0.1f)));
        Near(new Vector3(1, 0.3f, -0.1f), Hammer.Closest(cube, new Vector3(4, 0.3f, -0.1f)));
        Near(new Vector3(1, 1, 1), Hammer.Closest(cube, new Vector3(3, 3, 3)));
        // A kind the hammer does not know: the point itself.
        Near(new Vector3(7, 8, 9), Hammer.Closest(new Odd(), new Vector3(7, 8, 9)));
    }

    [Fact]
    public void ClosestPointOfATriangleInEachRegion()
    {
        Vector3 a = Vector3.Zero, b = Vector3.UnitX, c = Vector3.UnitZ;
        Near(a, Hammer.ClosestTriangle(new Vector3(-1, 0, -1), a, b, c));
        Near(new Vector3(0.5f, 0, 0), Hammer.ClosestTriangle(new Vector3(0.5f, 0, -1), a, b, c));
        Near(c, Hammer.ClosestTriangle(new Vector3(0, 0, 2), a, b, c));
        Near(new Vector3(0, 0, 0.5f), Hammer.ClosestTriangle(new Vector3(-1, 0, 0.5f), a, b, c));
        Near(new Vector3(0.5f, 0, 0.5f), Hammer.ClosestTriangle(new Vector3(1, 0, 1), a, b, c));
    }

    [Fact]
    public void RaysAgainstEachColliderKind()
    {
        var down = -Vector3.UnitY;
        // A sphere: hit on top, missed beside it, behind the ray and from inside.
        var (ts, ns) = Hammer.RayShape(new Vector3(0, 10, 0), down, new Hammer.Sphere(Vector3.Zero, 1))!.Value;
        Assert.Equal(9, ts, 3);
        Near(Vector3.UnitY, ns);
        Assert.Null(Hammer.RayShape(new Vector3(3, 10, 0), down, new Hammer.Sphere(Vector3.Zero, 1)));
        Assert.Null(Hammer.RayShape(new Vector3(0, -10, 0), down, new Hammer.Sphere(Vector3.Zero, 1)));
        Assert.Null(Hammer.RayShape(new Vector3(0, 0.5f, 0), down, new Hammer.Sphere(Vector3.Zero, 1)));
        // A capsule lying along x: hit on its side, missed past its end.
        var capsule = new Hammer.Capsule(new Vector3(-2, 0, 0), new Vector3(2, 0, 0), 0.5f);
        var (tc, nc) = Hammer.RayShape(new Vector3(1, 10, 0), down, capsule)!.Value;
        Assert.Equal(9.5f, tc, 2);
        Near(Vector3.UnitY, nc);
        Assert.Null(Hammer.RayShape(new Vector3(5, 10, 0), down, capsule));
        // From inside, even on its axis (where the normal was NaN, and Place made a NaN position of it).
        Assert.Null(Hammer.RayShape(Vector3.Zero, down, capsule));
        Assert.Null(Hammer.RayShape(new Vector3(0, 0.2f, 0), down, capsule));
        // A mesh: the nearest face, its normal against the ray; a ray beside it, or parallel to a face, misses.
        var (tm, nm) = Hammer.RayShape(new Vector3(0.2f, 10, 0.3f), down, Cube())!.Value;
        Assert.Equal(9, tm, 3);
        Near(Vector3.UnitY, nm);
        Assert.Null(Hammer.RayShape(new Vector3(3, 10, 0), down, Cube()));
        Assert.Null(Hammer.RayShape(new Vector3(-5, 1, 0.3f), Vector3.UnitX, new Hammer.Mesh(true, [Vector3.Zero, Vector3.UnitX, Vector3.UnitZ], [0, 1, 2])));
        // A box: missed beside it, parallel outside a slab, and from inside.
        var box = new Hammer.Box(Vector3.Zero, Vector3.One, Quaternion.Identity);
        Assert.Null(Hammer.RayShape(new Vector3(3, 10, 0), Vector3.Normalize(new Vector3(0.1f, -1, 0)), box));
        Assert.Null(Hammer.RayShape(new Vector3(3, 10, 0), down, box));
        Assert.Null(Hammer.RayShape(Vector3.Zero, down, box));
        Assert.Null(Hammer.RayShape(Vector3.Zero, down, new Odd()));
    }

    [Fact]
    public void APieceThatMayOverlapTurnedSnapsOntoOneTurnedOtherwise()
    {
        // stone_arch may stand where another does if turned differently (more than 10 degrees).
        var quarter = BlueprintFormats.FromEuler(new Vector3(0, 90, 0));
        var arch = new Hammer.Placed(0, "stone_arch", Vector3.Zero, None);
        var turned = new List<Hammer.Placed> { arch, new(1, "stone_arch", new Vector3(2, 0, 0), quarter) };
        var (pos, to) = Hammer.Snap("stone_arch", None, new Vector3(2.1f, 0, 0), turned);
        Near(new Vector3(2, 0, 0), pos);
        Assert.Equal(0, to);
        // Turned alike: refused, it stays where it was.
        var alike = new List<Hammer.Placed> { arch, new(1, "stone_arch", new Vector3(2, 0, 0), None) };
        var (stays, none) = Hammer.Snap("stone_arch", None, new Vector3(2.1f, 0, 0), alike);
        Assert.Null(none);
        Near(new Vector3(2.1f, 0, 0), stays);
    }

    /// <summary>
    /// A convex 2 m cube round the origin, as a mesh collider (12 triangles).
    /// </summary>
    private static Hammer.Mesh Cube()
    {
        Vector3[] v = [.. Enumerable.Range(0, 8).Select(i => new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))];
        int[] faces = [0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3];
        return new Hammer.Mesh(true, v, faces);
    }
}
