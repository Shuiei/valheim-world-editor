using TerrainEditor.App;
using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// The collider scans' tests (GameCataloguesPlaceTests, GameCataloguesSupportTests, GameCataloguesPieceTests) read a
/// made-up piece in a made-up game: the piece's root frame, and the game saved with one piece built and read back.
/// </summary>
internal static class ColliderTestGame
{
    /// <summary>
    /// A piece's root frame: at the origin, unturned and unscaled.
    /// </summary>
    public static GameCatalogues.ColliderFrame Unturned => new((0.0, 0.0, 0.0), GameCatalogues.Quat.Root, (1.0, 1.0, 1.0));

    /// <summary>
    /// A mesh of one triangle (the three corners of a unit square's half) in the bundle, for the mesh colliders.
    /// </summary>
    /// <param name="b">The bundle the mesh is in.</param>
    /// <returns>The mesh's path id.</returns>
    public static long Triangle(TestBundle b) => new TestMesh { Name = "wall", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]] }.Add(b);

    /// <summary>
    /// The game saved with a piece whose root has a mesh collider of <see cref="Triangle"/>, read back.
    /// </summary>
    /// <param name="game">The game (disposed by the caller).</param>
    /// <param name="collider">The mesh collider's path id.</param>
    /// <param name="mesh">The mesh's path id.</param>
    /// <returns>The saved game, read back: open its bundles by name.</returns>
    public static GameCatalogues.Game MeshPiece(TestGame game, out long collider, out long mesh)
    {
        var b = game.Bundle("p_colliders");
        long m = Triangle(b);
        mesh = m;
        return Piece(game, b, r => r.MeshCollider(m, convex: true), out collider);
    }

    /// <summary>
    /// Builds a piece in a bundle of the game with <paramref name="build"/> (it returns the path id it wants back, a
    /// collider), saves the game and reads its bundles back.
    /// </summary>
    /// <param name="game">The game the bundle is in (disposed by the caller).</param>
    /// <param name="b">The bundle the piece is built in.</param>
    /// <param name="build">The piece's root, built; returns the path id of the object the test reads.</param>
    /// <param name="pid">The path id <paramref name="build"/> returned.</param>
    /// <returns>The saved game, read back: open its bundles by name.</returns>
    public static GameCatalogues.Game Piece(TestGame game, TestBundle b, Func<PrefabNode, long> build, out long pid)
    {
        long made = 0;
        b.Prefab("piece", r => made = build(r));
        pid = made;
        game.Save();
        return new GameCatalogues.Game(game.BundlesDir);
    }

    /// <summary>
    /// <see cref="Piece"/> for a building piece the support scan reads: its root has a Piece and a wood WearNTear that
    /// supports, then what <paramref name="colliders"/> adds.
    /// </summary>
    /// <param name="game">The game the bundle is in (disposed by the caller).</param>
    /// <param name="b">The bundle the piece is built in.</param>
    /// <param name="colliders">Adds the piece's colliders to its root; returns the path id of the object the test reads.</param>
    /// <param name="pid">The path id <paramref name="colliders"/> returned.</param>
    /// <returns>The saved game, read back: open its bundles by name.</returns>
    public static GameCatalogues.Game WoodPiece(TestGame game, TestBundle b, Func<PrefabNode, long> colliders, out long pid) =>
        Piece(game, b, r =>
        {
            r.Mono("Piece", new Fields { ["m_category"] = 2, ["m_resources"] = new List<Fields>() });
            r.Mono("WearNTear", new Fields { ["m_materialType"] = 0, ["m_supports"] = true });
            return colliders(r);
        }, out pid);
}
