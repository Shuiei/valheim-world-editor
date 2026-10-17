using AssetsTools.NET.Extra;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The support scan's collider bounds (SupportScan): a box's, a sphere's, a capsule's and a mesh's centre and half size
/// in their own frame, the aabb flag, and a box written with its layer. Made-up colliders (TestGame), hand-picked numbers.
/// </summary>
public class GameCataloguesSupportTests
{
    [Fact]
    public void ABoxHasHalfItsSizeAtItsCentreWithAabbZero()
    {
        // Centre (1, 2, 3), size (4, 6, 8): half sizes 2, 3 and 4.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Box((1, 2, 3), (4, 6, 8)), out long box);
        var (centre, half, aabb) = GameCatalogues.SupportScan.BoxBounds(g.Open("p_colliders")!.Field(box)!);
        Assert.Equal((1.0, 2.0, 3.0), centre);
        Assert.Equal((2.0, 3.0, 4.0), half);
        Assert.Equal(0L, aabb);
    }

    [Fact]
    public void ACapsuleReachesHalfItsHeightAlongItsDirection()
    {
        // Radius 0.25, height 2 along y: the half height is 1, the other axes the radius.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Capsule((0, 0, 0), 0.25, 2, 1), out long capsule);
        var (_, half, aabb) = GameCatalogues.SupportScan.CapsuleBounds(g.Open("p_colliders")!.Field(capsule)!);
        Assert.Equal((0.25, 1.0, 0.25), half);
        Assert.Equal(1L, aabb);
    }

    [Fact]
    public void ACapsuleIsAtLeastItsRadiusHalfHigh()
    {
        // Height 0.2 is shorter than its radius allows: the half height is the radius, 0.25.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Capsule((0, 0, 0), 0.25, 0.2, 1), out long capsule);
        var (_, half, _) = GameCatalogues.SupportScan.CapsuleBounds(g.Open("p_colliders")!.Field(capsule)!);
        Assert.Equal((0.25, 0.25, 0.25), half);
    }

    [Fact]
    public void ASphereHasItsRadiusInEveryAxisWithAabbOne()
    {
        // Centre (1, 2, 3), radius 0.5.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_colliders"), r => r.Sphere((1, 2, 3), 0.5), out long sphere);
        var bounds = new GameCatalogues.SupportScan().ColliderBounds((int)AssetClassID.SphereCollider, g.Open("p_colliders")!.Field(sphere)!, null);
        Assert.NotNull(bounds);
        Assert.Equal(((1.0, 2.0, 3.0), (0.5, 0.5, 0.5), 1L), (bounds.Value.C, bounds.Value.H, bounds.Value.Aabb));
    }

    [Fact]
    public void AMeshNotReadYetIsOnlyNotedWithNoBounds()
    {
        // The mesh's id is noted for its first read; there are no bounds until the bundle's meshes are read.
        using var game = new TestGame();
        var b = game.Bundle("p_colliders");
        long mesh = ColliderTestGame.Triangle(b);
        using var g = ColliderTestGame.Piece(game, b, r => r.MeshCollider(mesh, convex: true), out long collider);
        var meshes = new List<long>();
        var bounds = new GameCatalogues.SupportScan().MeshBounds(g.Open("p_colliders")!.Field(collider)!, meshes);
        Assert.Null(bounds);
        Assert.Equal(new[] { mesh }, meshes);
    }

    [Fact]
    public void AMeshFromAnotherFileHasNoBoundsAndIsNotNoted()
    {
        // The mesh is in another bundle: its bounds are not read here, so it is neither noted nor given bounds.
        using var game = new TestGame();
        var other = game.Bundle("m_meshes");
        long mesh = ColliderTestGame.Triangle(other);
        var b = game.Bundle("p_colliders");
        using var g = ColliderTestGame.Piece(game, b, r => r.Component(UnityClass.MeshCollider, UnityTypes.MeshCollider, new Fields { ["m_Mesh"] = b.RefTo(other, mesh) }), out long collider);
        var meshes = new List<long>();
        var bounds = new GameCatalogues.SupportScan().MeshBounds(g.Open("p_colliders")!.Field(collider)!, meshes);
        Assert.Null(bounds);
        Assert.Empty(meshes);
    }

    [Fact]
    public void AMeshReadHasTheBoundsOfItsAabb()
    {
        // A piece with a WearNTear, its mesh collider on the root: read, the mesh's AABB centre and extent are its bounds.
        using var game = new TestGame();
        var b = game.Bundle("p_pieces");
        long mesh = new TestMesh { Name = "wall", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((0.5, 0.5, 0), (0.5, 0.5, 0)) }.Add(b);
        using var g = ColliderTestGame.Piece(game, b, r =>
        {
            r.Mono("Piece", new Fields { ["m_category"] = 2, ["m_resources"] = new List<Fields>() });
            r.Mono("WearNTear", new Fields { ["m_materialType"] = 0, ["m_supports"] = true });
            return r.MeshCollider(mesh, convex: true);
        }, out long collider);
        var bundle = g.Open("p_pieces")!;
        var scan = new GameCatalogues.SupportScan();
        scan.Read(bundle, g);
        var bounds = scan.MeshBounds(bundle.Field(collider)!, null);
        Assert.NotNull(bounds);
        Assert.Equal(((0.5, 0.5, 0.0), (0.5, 0.5, 0.0), 1L), (bounds.Value.C, bounds.Value.H, bounds.Value.Aabb));
    }

    [Fact]
    public void ABoxIsWrittenWithItsBoundsInTheFrameThenItsAabbAndLayer()
    {
        // Bounds as a box is given (centre (1, 2, 3), half sizes (2, 3, 4), aabb 0) in an unturned frame, on layer 10.
        var boxes = new List<object?>();
        GameCatalogues.SupportScan.AddBox(((1.0, 2.0, 3.0), (2.0, 3.0, 4.0), 0L), ColliderTestGame.Unturned, 10L, boxes);
        Assert.Equal("[[1.0,2.0,3.0,2.0,3.0,4.0,0,0,0,1,0,10]]", CatalogueJson.Write(boxes, new CatalogueJson.Style(",", ":")));
    }
}
