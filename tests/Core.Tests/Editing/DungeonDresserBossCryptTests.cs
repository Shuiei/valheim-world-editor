using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The crypt arena of a boss room (DungeonDresser.BossCrypt): the arena's grid holds the sarcophagi between its columns.
/// </summary>
public class DungeonDresserBossCryptTests
{
    [Fact]
    public void TheCryptArenaPlacesSarcophagiBetweenItsColumns()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var (dresser, o) = DungeonDresserInstance.Of(room);

        PrivateCall.Instance(dresser, "CryptGrid", room.Frame);

        Assert.Contains(o.Items, i => i.Prefab == "crypt_skeleton_chest");
    }
}
