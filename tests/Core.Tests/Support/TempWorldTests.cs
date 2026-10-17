using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The test world's copy (TempWorld) cleaned up: a copy already gone is nothing to do, and a copy that cannot be
/// deleted fails the clean-up instead of being left behind unseen.
/// </summary>
public sealed class TempWorldTests
{
    [Fact]
    public void ACopyAlreadyGoneIsNothingToCleanUp()
    {
        var world = new TempWorld();
        Directory.Delete(Path.GetDirectoryName(world.Dir)!, true);

        world.Dispose();

        Assert.False(Directory.Exists(world.Dir));
    }

    [Fact]
    public void ACopyThatCannotBeDeletedFailsTheCleanUp()
    {
        // Root deletes whatever the folders' permissions (CI's containers run as root).
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            Assert.Skip("only a user other than root is refused a folder by its permissions");
            return;
        }
        var world = new TempWorld();
        string locked = Path.Combine(world.Dir, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "held"), "");
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var e = Record.Exception(world.Dispose);

            Assert.True(e is UnauthorizedAccessException or IOException, $"the clean-up did not fail: {e}");
            Assert.True(File.Exists(Path.Combine(locked, "held")));
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            world.Dispose();
        }
    }
}
