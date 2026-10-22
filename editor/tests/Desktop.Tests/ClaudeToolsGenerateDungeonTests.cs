using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Claude's generate_dungeon (ClaudeTools.GenerateDungeon): a biome, style or wall material the generator does not have
/// is refused with those it has, not quietly replaced.
/// </summary>
public class ClaudeToolsGenerateDungeonTests
{
    [Fact]
    public void ABiomeStyleOrWallsNotKnownIsSaidWithTheKnownOnes()
    {
        Assert.Null(ClaudeTools.UnknownDungeonChoice("black forest", null, null));
        Assert.Null(ClaudeTools.UnknownDungeonChoice("Mountain", "Fortress", "Stone"));
        Assert.Null(ClaudeTools.UnknownDungeonChoice("Swamp", "", " "));
        Assert.StartsWith("No dungeon biome \"Forest\": Meadows, Black Forest, Swamp", ClaudeTools.UnknownDungeonChoice("Forest", null, null));
        Assert.StartsWith("No dungeon style \"Castle\": Crypt, Catacombs", ClaudeTools.UnknownDungeonChoice("Swamp", "Castle", null));
        Assert.StartsWith("No dungeon wall material \"Glass\": Stone, Marble", ClaudeTools.UnknownDungeonChoice("Swamp", null, "Glass"));
    }
}
