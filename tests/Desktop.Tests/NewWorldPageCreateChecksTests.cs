using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Whether a new world can be created with a name and a seed: the name must be a valid world name, the seed one to ten
/// characters, and no world of that name may be in the folder already. A world that can be made gives no reason.
/// </summary>
public sealed class NewWorldPageCreateChecksTests : IDisposable
{
    /// <summary>A folder for the worlds, made fresh for each test.</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vwe-new-world-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Makes the worlds folder.</summary>
    public NewWorldPageCreateChecksTests() => Directory.CreateDirectory(_root);

    /// <summary>Deletes the worlds folder.</summary>
    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>Calls the new world check with the test folder.</summary>
    private Said? WhyNot(string name, string seed) =>
        (Said?)PrivateMethods.Call(typeof(NewWorldPage), "WhyNot", null, _root, name, seed);

    /// <summary>A name that is not a valid world name is refused with the name message.</summary>
    [Fact]
    public void AnInvalidNameIsRefused() =>
        Assert.Equal(Said.Of("newWorld.error.name"), WhyNot("bad.", "abc"));

    /// <summary>A seed of no characters is refused with the seed message.</summary>
    [Fact]
    public void AnEmptySeedIsRefused() =>
        Assert.Equal(Said.Of("newWorld.error.seedLength"), WhyNot("Island", ""));

    /// <summary>A seed of eleven characters is refused, while ten are allowed.</summary>
    [Fact]
    public void AnElevenCharacterSeedIsRefusedAndTenAreAllowed()
    {
        Assert.Equal(Said.Of("newWorld.error.seedLength"), WhyNot("Island", "12345678901"));
        Assert.Null(WhyNot("Island", "1234567890"));
    }

    /// <summary>A world of that name already in the folder is refused, whether a folder or a .fwl file.</summary>
    [Fact]
    public void AnExistingWorldFolderIsRefused()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Island"));

        Assert.Equal(Said.Of("newWorld.error.exists", "Island", Ui.Tilde(_root)), WhyNot("Island", "abc"));
    }

    /// <summary>A world file of that name already in the folder is refused too.</summary>
    [Fact]
    public void AnExistingWorldFileIsRefused()
    {
        File.WriteAllText(Path.Combine(_root, "Island.fwl"), "");

        Assert.Equal(Said.Of("newWorld.error.exists", "Island", Ui.Tilde(_root)), WhyNot("Island", "abc"));
    }

    /// <summary>A valid name and seed with no world of that name is no reason to refuse.</summary>
    [Fact]
    public void AValidNameAndSeedWithNoWorldGivesNoReason() =>
        Assert.Null(WhyNot("Island", "abc"));

}
