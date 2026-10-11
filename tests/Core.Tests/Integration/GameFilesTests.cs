using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's own files read on a computer with Valheim (skipped elsewhere, as in CI): the terrain and the map. Run
/// alone: dotnet test tests/Core.Tests --filter Category=Game
/// </summary>
[Trait("Category", "Game")]
public class GameFilesTests
{
    // The terrain (its shader from both programs, the Linux copy's OpenGL one and the Vulkan one
    // Valheim for Windows has) and the map, from the game's own files.
    [Fact]
    public void TheTerrainAndMapAreReadFromTheGame()
    {
        string? valheim = GameLook.FindValheim(null);
        Assert.SkipUnless(valheim != null, "needs Valheim");
        var game = GameBundles.ForGame(valheim, GameLook.IndexFile)!;
        var vulkan = GameLookData.ReadTerrain(game, vulkan: true);
        Assert.Contains("void valheimGBuffer()", vulkan.Fragment);
        Assert.Equal(256, vulkan.DiffuseArray.Width);
        Assert.Equal(0, vulkan.DiffuseArray.Height % 256);
        Assert.Equal(GameLookData.TerrainTextures.Count, vulkan.Textures.Count);
        var programs = game.LoadShader(game.LoadMaterial(game.Asset("Heightmap_basematerial.mat")!)!.ShaderAt!)!;
        if (programs.Platforms.Contains(GameShader.OpenGlCore))
        {
            Assert.Contains("void valheimGBuffer()", GameLookData.ReadTerrain(game).Fragment);
        }
        var map = GameLookData.ReadMap(game);
        Assert.Equal(GameLookData.MapTextures.Count, map.Count);
        Assert.All(map.Values, p => Assert.Equal(p.Width * p.Height * 4, p.Rgba.Length));
    }
}
