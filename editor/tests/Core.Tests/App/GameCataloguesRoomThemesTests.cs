using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The room themes read from a game's code (GameCatalogues.RoomThemesIn): the named values of an enum Theme nested in
/// a class Room, none from code without one.
/// </summary>
public class GameCataloguesRoomThemesTests
{
    /// <summary>A stand-in for the game's class, read from this assembly's metadata.</summary>
    public static class Room
    {
        /// <summary>Its themes.</summary>
        public enum Theme
        {
            /// <summary>No theme.</summary>
            None = 0,
            /// <summary>A theme.</summary>
            Crypt = 1,
            /// <summary>A high theme.</summary>
            MorkHalla = 0x20000,
        }
    }

    [Fact]
    public void TheNestedEnumsNamesAndValuesAreRead()
    {
        var themes = GameCatalogues.RoomThemesIn(typeof(GameCataloguesRoomThemesTests).Assembly.Location);
        Assert.Equal([("None", 0L), ("Crypt", 1L), ("MorkHalla", 131072L)], themes.Select(kv => (kv.Key, (long)kv.Value!)));
    }

    [Fact]
    public void CodeWithoutItHasNone() => Assert.Empty(GameCatalogues.RoomThemesIn(typeof(GameCatalogues).Assembly.Location));
}
