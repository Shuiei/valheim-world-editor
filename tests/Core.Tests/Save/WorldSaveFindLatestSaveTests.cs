using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Choosing the save a world loads: the committed one, and an error when the folder holds no save index at all.
/// </summary>
public class WorldSaveFindLatestSaveTests
{
    [Fact]
    public void AFolderWithNoSaveIndexIsRefused()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-latest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Throws<InvalidDataException>(() => PrivateMember.Call(typeof(WorldSave), "FindLatestSave", null, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TheCommittedSaveIsLoadedRatherThanAnUncommittedNewerOne()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-latest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "_main.2.chunks"), "");
            File.WriteAllText(Path.Combine(dir, "_main.2.ok"), "");
            File.WriteAllText(Path.Combine(dir, "_main.3.chunks"), "");

            Assert.Equal(2, PrivateMember.Call<int>(typeof(WorldSave), "FindLatestSave", null, dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
