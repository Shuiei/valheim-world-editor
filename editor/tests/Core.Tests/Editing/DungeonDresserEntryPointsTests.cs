using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The names a room's hall dressing takes: a variant the dresser does not know is refused (DungeonDresser.DressHallAs).
/// </summary>
public class DungeonDresserEntryPointsTests
{
    [Fact]
    public void AHallVariantTheDresserDoesNotKnowIsAnArgumentError()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");

        Assert.Throws<ArgumentException>(() => DungeonDresserSupport.DressHall(room, "no such variant"));
    }
}
