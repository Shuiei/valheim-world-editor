using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Looking for Valheim outside the home too (GameLook.SearchOutsideHome, as the editor does): Steam's system-wide folders
/// are searched as well, and whatever is found there is the game's folder. On a pretend home, put back afterwards.
/// </summary>
[Collection("DataDir")]
public sealed class GameLookOutsideHomeTests : IDisposable
{
    private readonly GameHome _sandbox = new("vwe-outside-home-");

    /// <summary>Puts the environment, the search and the settings back, and deletes the pretend home.</summary>
    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void TheSystemsSteamFoldersAreSearchedToo()
    {
        if (OperatingSystem.IsWindows())
        {
            // Steam's folder comes from the registry there, and the pretend home cannot point it elsewhere.
            return;
        }
        GameLook.SearchOutsideHome = true;

        string? found = GameLook.FindValheim(null);

        // None on most computers (CI); a system-wide Steam with the game gives its folder.
        Assert.True(found == null || GameLook.BundlesDir(found) != null, found);
    }
}
