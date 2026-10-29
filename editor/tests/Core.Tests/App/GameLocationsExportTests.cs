using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The tests' location copy made again from the game (GameLocations.Export), on a pretend home: without the game nothing
/// is written, and with the game set in the settings the prioritized rules read from its main scene are.
/// </summary>
[Collection("DataDir")]
public sealed class GameLocationsExportTests : IDisposable
{
    private readonly GameHome _sandbox = new("vwe-locations-export-");

    /// <summary>Puts the environment and settings back, and deletes the pretend home.</summary>
    public void Dispose() => _sandbox.Dispose();

    [Fact]
    public void WithoutTheGameNothingIsWritten()
    {
        string file = Path.Combine(_sandbox.Folder, "locations.json");

        Assert.Equal(1, GameLocations.Export(file));

        Assert.False(File.Exists(file));
    }

    [Fact]
    public void TheRulesOfTheGameSetInTheSettingsAreWritten()
    {
        using var game = SceneGame.Make(GameLocations.ListOrder().Order);
        game.Save();
        Assert.Null(new AppSettings { ValheimPath = game.Root }.Save());
        string file = Path.Combine(_sandbox.Folder, "locations.json");

        Assert.Equal(0, GameLocations.Export(file));

        string text = File.ReadAllText(file);
        Assert.Contains("StartTemple", text, StringComparison.Ordinal);
        // The rules laid out first only: the plain crypts are not among them.
        Assert.DoesNotContain("Crypt_old", text, StringComparison.Ordinal);
    }
}
