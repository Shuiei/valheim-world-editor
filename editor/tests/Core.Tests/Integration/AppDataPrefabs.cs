using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The prefab hashes of the AppData tests (the test world's natural objects, as the game names them).
/// </summary>
internal static class AppDataPrefabs
{
    /// <summary>
    /// The prefab hash of a beech tree.
    /// </summary>
    public static int Beech => StableHash.Of("Beech1");
}
