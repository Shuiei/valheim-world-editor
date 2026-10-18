using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// The made-up meshes and texture formats the GameBundles tests share (GameBundles*Tests): a quad, and the RGBA32 and
/// Alpha8 texture formats used.
/// </summary>
public static class GameBundlesTestAssets
{
    /// <summary>
    /// The texture formats used: RGBA32 and Alpha8.
    /// </summary>
    public const int Rgba32 = 4, Alpha8 = 1;

    /// <summary>
    /// A two-triangle quad mesh (4 vertices), in one submesh unless split in two.
    /// </summary>
    public static TestMesh Quad(string name, bool split = false) => new()
    {
        Name = name,
        Positions = [0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0],
        Submeshes = split ? [[0, 1, 2], [0, 2, 3]] : [[0, 1, 2, 0, 2, 3]],
    };
}
