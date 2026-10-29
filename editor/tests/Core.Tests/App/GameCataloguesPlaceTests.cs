using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The placement scan's collider writers (PlaceScan): a box, a sphere, a capsule and a mesh each written in a piece's
/// frame, and the colliders of an object written in their order. Made-up pieces (TestGame), read as the game's are; the
/// frames are hand-picked, so the numbers are worked out by hand in each test.
/// </summary>
public class GameCataloguesPlaceTests
{
    /// <summary>
    /// The JSON of what a scan wrote (the way the catalogue writes it).
    /// </summary>
    private static string Json(object? written) => CatalogueJson.Write(written, new CatalogueJson.Style(",", ":"));

    [Fact]
    public void ABoxIsItsCentreAndHalfSizesAndTheRotationOfItsFrame()
    {
        // Centre (1, 2, 3), size (4, 6, 8): the half sizes are 2, 3 and 4.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Box((1, 2, 3), (4, 6, 8)), out long box);
        var cols = new List<object?>();
        GameCatalogues.PlaceScan.AddBox(g.Open("p_colliders")!.Field(box)!, ColliderTestGame.Unturned, cols);
        Assert.Equal("[[\"b\",1.0,2.0,3.0,2.0,3.0,4.0,0,0,0,1]]", Json(cols));
    }

    [Fact]
    public void ABoxScalesItsCentreAndHalfSizesByTheFrame()
    {
        // The frame scales by (2, 1, 0.5): the centre becomes (2, 2, 1.5), the half sizes (4, 3, 2).
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Box((1, 2, 3), (4, 6, 8)), out long box);
        var frame = new GameCatalogues.ColliderFrame((0.0, 0.0, 0.0), GameCatalogues.Quat.Root, (2.0, 1.0, 0.5));
        var cols = new List<object?>();
        GameCatalogues.PlaceScan.AddBox(g.Open("p_colliders")!.Field(box)!, frame, cols);
        Assert.Equal("[[\"b\",2.0,2.0,1.5,4.0,3.0,2.0,0,0,0,1]]", Json(cols));
    }

    [Fact]
    public void ASphereHasItsRadiusScaledByTheLargestAxisOfTheFrame()
    {
        // Centre (1, 2, 3) becomes (2, 2, 12) under the scale (2, 1, 4); radius 0.5 times the largest axis, 4, is 2.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Sphere((1, 2, 3), 0.5), out long sphere);
        var frame = new GameCatalogues.ColliderFrame((0.0, 0.0, 0.0), GameCatalogues.Quat.Root, (2.0, 1.0, 4.0));
        var cols = new List<object?>();
        GameCatalogues.PlaceScan.AddSphere(g.Open("p_colliders")!.Field(sphere)!, frame, cols);
        Assert.Equal("[[\"s\",2.0,2.0,12.0,2.0]]", Json(cols));
    }

    [Fact]
    public void ACapsuleIsItsSegmentAlongItsDirection()
    {
        // Radius 0.25, height 2 along y: the half length left for the segment is 1 - 0.25 = 0.75, from y -0.75 to 0.75.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Capsule((0, 0, 0), 0.25, 2, 1), out long capsule);
        var cols = new List<object?>();
        GameCatalogues.PlaceScan.AddCapsule(g.Open("p_colliders")!.Field(capsule)!, ColliderTestGame.Unturned, cols);
        Assert.Equal("[[\"c\",0.0,0.75,0.0,0.0,-0.75,0.0,0.25]]", Json(cols));
    }

    [Fact]
    public void AMeshNotYetWantedIsOnlyNotedForItsFirstRead()
    {
        // Without a list to write into (only the meshes wanted): the mesh's id is noted, nothing else.
        using var game = new TestGame();
        using var g = ColliderTestGame.MeshPiece(game, out long collider, out long mesh);
        var c = g.Open("p_colliders")!.Field(collider)!;
        var meshes = new List<long>();
        new GameCatalogues.PlaceScan().AddMesh(c, c["m_Mesh"], ColliderTestGame.Unturned, null, meshes);
        Assert.Equal(new[] { mesh }, meshes);
    }

    [Fact]
    public void AMeshNotReadYetWritesNothing()
    {
        // The mesh is noted for the first read, and no collider is written until it has been read.
        using var game = new TestGame();
        using var g = ColliderTestGame.MeshPiece(game, out long collider, out long mesh);
        var c = g.Open("p_colliders")!.Field(collider)!;
        var cols = new List<object?>();
        var meshes = new List<long>();
        new GameCatalogues.PlaceScan().AddMesh(c, c["m_Mesh"], ColliderTestGame.Unturned, cols, meshes);
        Assert.Empty(cols);
        Assert.Equal(new[] { mesh }, meshes);
    }

    [Fact]
    public void AMeshReadIsWrittenWithItsVerticesAndTriangles()
    {
        // The piece is read first (its mesh is read with it), then the collider is written in the piece's frame: the
        // vertices as they are, the convex flag as 1.
        using var game = new TestGame();
        var b = game.Bundle("p_pieces");
        long mesh = ColliderTestGame.Triangle(b);
        using var g = ColliderTestGame.Piece(game, b, r =>
        {
            r.Layer = 10;
            r.Mono("Piece", new Fields { ["m_category"] = 2, ["m_resources"] = new List<Fields>() });
            return r.MeshCollider(mesh, convex: true);
        }, out long collider);
        var bundle = g.Open("p_pieces")!;
        var scan = new GameCatalogues.PlaceScan();
        scan.Read(bundle, g);
        var c = bundle.Field(collider)!;
        var cols = new List<object?>();
        scan.AddMesh(c, c["m_Mesh"], ColliderTestGame.Unturned, cols, null);
        Assert.Equal("[[\"m\",1,[0.0,0.0,0.0,1.0,0.0,0.0,0.0,1.0,0.0],[0,1,2]]]", Json(cols));
    }

    [Fact]
    public void TheCollidersOfAnObjectAreWrittenInTheirOrder()
    {
        // A box then a sphere: each written by its kind, in the order the scan meets them.
        using var game = new TestGame();
        long box = 0, sphere = 0;
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r =>
        {
            box = r.Box((1, 2, 3), (4, 6, 8));
            sphere = r.Sphere((0, 0, 0), 1);
            return box;
        }, out _);
        var bundle = g.Open("p_colliders")!;
        List<(int Type, AssetTypeValueField F)> colliders = [((int)AssetClassID.BoxCollider, bundle.Field(box)!), ((int)AssetClassID.SphereCollider, bundle.Field(sphere)!)];
        var cols = new List<object?>();
        new GameCatalogues.PlaceScan().AddColliders(bundle, colliders, ColliderTestGame.Unturned, cols, null);
        Assert.Equal("[[\"b\",1.0,2.0,3.0,2.0,3.0,4.0,0,0,0,1],[\"s\",0.0,0.0,0.0,1.0]]", Json(cols));
    }
}
