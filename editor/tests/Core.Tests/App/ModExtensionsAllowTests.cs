using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Allowing an extension and taking that away (ModExtensions.Allow): the DLL's hash kept in the settings, or dropped; and
/// the hash of a file that cannot be read, which allows nothing.
/// </summary>
public class ModExtensionsAllowTests
{
    [Fact]
    public void AllowingKeepsTheHashAndTakingItAwayDropsIt()
    {
        var settings = new AppSettings();
        var extension = new ModExtensions.Extension("/mods/Fine/Fine.WorldEditor.dll", "Fine", "abc123", ModExtensions.Status.Off, null);

        ModExtensions.Allow(settings, extension, true);
        Assert.Equal("abc123", settings.Extensions[extension.Dll]);
        ModExtensions.Allow(settings, extension, false);
        Assert.Empty(settings.Extensions);
        // Taken away again: nothing to drop.
        ModExtensions.Allow(settings, extension, false);
        Assert.Empty(settings.Extensions);
    }

    [Fact]
    public void AFileWithoutAHashCannotBeAllowed()
    {
        var settings = new AppSettings();
        var unread = new ModExtensions.Extension("/mods/Gone/Gone.dll", "Gone", "", ModExtensions.Status.Off, "the file cannot be read");

        ModExtensions.Allow(settings, unread, true);

        Assert.Empty(settings.Extensions);
    }

    [Fact]
    public void TheHashOfAFileThatCannotBeReadIsEmpty()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-ext-hash-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string file = Path.Combine(dir, "Some.dll");
            File.WriteAllBytes(file, [1, 2, 3]);
            Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData([1, 2, 3])), ModExtensions.Sha256Of(file));
            Assert.Equal("", ModExtensions.Sha256Of(Path.Combine(dir, "missing.dll")));
            // A folder in its place.
            Assert.Equal("", ModExtensions.Sha256Of(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
