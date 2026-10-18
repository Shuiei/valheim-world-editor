using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The order of the game's location lists read from the game's own log in the pretend home (GameLocations.ListOrder):
/// the newest log that has it, one that cannot be read passed over, and the editor's table when no log has it.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderGameLogTests : HomeFolderTestBase
{
    /// <summary>A line of the game's log: a list added at the game's start.</summary>
    private static string Added(int locations, int vegetation) =>
        $"10/10/2026 06:59:10: Added {locations} locations, {vegetation} vegetations, 0 environments, 0 biome env-setups, 0 clutter  from main";

    /// <summary>The folder the game's logs lie in on Linux (beside its saves), made.</summary>
    private string LogFolder()
    {
        string folder = Local(".config", "unity3d", "IronGate", "Valheim");
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public void TheOrderComesFromTheNewestLogThatHasIt()
    {
        if (!LinuxLayout)
        {
            return;
        }
        string folder = LogFolder();
        // The newest log has no list lines yet (the game was quit before a world loaded): the one before is read.
        File.WriteAllLines(Path.Combine(folder, "Player-prev.log"), ["Zonesystem Start 1", Added(3, 0), Added(5, 1)]);
        File.SetLastWriteTimeUtc(Path.Combine(folder, "Player-prev.log"), DateTime.UtcNow.AddMinutes(-5));
        File.WriteAllLines(Path.Combine(folder, "Player.log"), ["nothing yet"]);

        var (order, from) = GameLocations.ListOrder();

        Assert.Equal([(3, 0), (5, 1)], order);
        Assert.StartsWith(Path.Combine(folder, "Player-prev.log"), from);
    }

    [Fact]
    public void ALogThatCannotBeReadIsPassedOver()
    {
        if (OperatingSystem.IsWindows() || !LinuxLayout || Environment.UserName == "root")
        {
            return;
        }
        string folder = LogFolder();
        string locked = Path.Combine(folder, "Player.log");
        File.WriteAllLines(locked, [Added(9, 9)]);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        File.WriteAllLines(Path.Combine(folder, "Player-prev.log"), [Added(3, 0)]);
        File.SetLastWriteTimeUtc(Path.Combine(folder, "Player-prev.log"), DateTime.UtcNow.AddMinutes(-5));

        var (order, from) = GameLocations.ListOrder();

        Assert.Equal([(3, 0)], order);
        Assert.StartsWith(Path.Combine(folder, "Player-prev.log"), from);
    }

    [Fact]
    public void WithoutALogOfTheGameTheEditorsTableIsUsed()
    {
        if (!LinuxLayout)
        {
            return;
        }
        var (order, from) = GameLocations.ListOrder();

        Assert.Equal(6, order.Length);
        Assert.Contains("the editor's table", from);
    }
}
