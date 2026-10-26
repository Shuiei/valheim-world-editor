using System.Text.Json.Nodes;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A mesh whose vertices the bundle keeps in its resource file (GameCatalogues.Bundle.StreamBytes): a piece's mesh
/// collider read from there gives the same shape as one kept in the object.
/// </summary>
public class GameCataloguesBundleStreamsTests
{
    [Fact]
    public void AStreamedMeshColliderIsReadFromTheResourceFile()
    {
        using var game = new TestGame();
        var b = game.Bundle("pieces");
        long streamed = new TestMesh { Name = "wall_collider", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((0.5, 0.5, 0), (0.5, 0.5, 0)), Streamed = true }.Add(b);
        long kept = new TestMesh { Name = "wall_kept", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((0.5, 0.5, 0), (0.5, 0.5, 0)) }.Add(b);
        foreach (var (name, mesh) in new[] { ("wall_streamed", streamed), ("wall_in_object", kept) })
        {
            b.Prefab(name, r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("Piece", new Fields { ["m_category"] = 2, ["m_resources"] = new List<Fields>() });
                r.Child("collider", c => c.MeshCollider(mesh, convex: true));
            });
        }
        game.Save();

        var place = JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["piece-place"])["piece-place"])!.AsObject();

        string fromResource = place["wall_streamed"]!["c"]!.ToJsonString();
        Assert.Contains("\"m\"", fromResource);
        Assert.Equal(place["wall_in_object"]!["c"]!.ToJsonString(), fromResource);
    }
}
