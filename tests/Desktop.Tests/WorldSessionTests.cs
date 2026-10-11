using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Worlds opened in the app (WorldSession): a world just made from a seed (WorldCreator) opens with its objects.
/// </summary>
[Collection("World files")]
public sealed class WorldSessionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-session-" + Guid.NewGuid().ToString("N")[..8]);

    // The test world's seed (tests/fixtures/CITest).
    private const string Seed = "GbXfypqU3G";

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// A folder inside the temporary folder.
    /// </summary>
    private string Folder(string name) => Path.Combine(_dir, name);

    [Fact]
    public void TheNativeAppOpensIt()
    {
        WorldCreator.Create(Folder("Open"), "Open", Seed, radius: 1);
        var world = WorldSession.Open(Folder("Open"));
        var scene = WorldScene.Load(world, 0, 0, 1);
        Assert.NotEmpty(scene.Things);
        Assert.Equal("Open", world.World.Name);
    }
}
