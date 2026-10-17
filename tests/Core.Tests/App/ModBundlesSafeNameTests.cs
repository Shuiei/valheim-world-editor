using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The names of the bundles a mod folder holds: a name keeps its letters, digits, dots, dashes and underscores and every
/// other character becomes an underscore, and a file starts with the UnityFS signature or it is not a bundle.
/// </summary>
public sealed class ModBundlesSafeNameTests : IDisposable
{
    /// <summary>A folder for the test files, made fresh for each test.</summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vwe-bundles-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Makes the test folder.</summary>
    public ModBundlesSafeNameTests() => Directory.CreateDirectory(_folder);

    /// <summary>Deletes the test folder.</summary>
    public void Dispose() => Directory.Delete(_folder, true);

    /// <summary>Calls the bundle name's safe-name helper.</summary>
    private static string Safe(string name) => (string)PrivateMember.Call(typeof(ModBundles), "Safe", null, name)!;

    /// <summary>Letters, digits, dots, dashes and underscores are kept as they are.</summary>
    [Fact]
    public void LettersDigitsDotsDashesAndUnderscoresAreKept() =>
        Assert.Equal("ok-name_1.dll", Safe("ok-name_1.dll"));

    /// <summary>A space or a slash becomes an underscore, so the name is one file name.</summary>
    [Fact]
    public void SpacesAndSlashesBecomeUnderscores() =>
        Assert.Equal("a_b_c.d", Safe("a b/c.d"));

    /// <summary>A letter outside ASCII is not an ASCII letter, so it becomes an underscore.</summary>
    [Fact]
    public void ANonAsciiLetterBecomesAnUnderscore() =>
        Assert.Equal("caf_", Safe("café"));

    /// <summary>A file that starts with the UnityFS signature is a bundle.</summary>
    [Fact]
    public void AFileStartingWithTheSignatureIsABundle()
    {
        string file = Path.Combine(_folder, "bundle.unity3d");
        File.WriteAllBytes(file, "UnityFS\0\0\0\0"u8.ToArray());

        Assert.True((bool)PrivateMember.Call(typeof(ModBundles), "StartsWithMagic", null, file)!);
    }

    /// <summary>A file that starts with other bytes is not a bundle.</summary>
    [Fact]
    public void AFileWithOtherBytesIsNotABundle()
    {
        string file = Path.Combine(_folder, "other.bin");
        File.WriteAllBytes(file, "Unity Web\0\0\0\0"u8.ToArray());

        Assert.False((bool)PrivateMember.Call(typeof(ModBundles), "StartsWithMagic", null, file)!);
    }

    /// <summary>A file shorter than the signature is not a bundle.</summary>
    [Fact]
    public void AFileShorterThanTheSignatureIsNotABundle()
    {
        string file = Path.Combine(_folder, "short.bin");
        File.WriteAllBytes(file, "Unity"u8.ToArray());

        Assert.False((bool)PrivateMember.Call(typeof(ModBundles), "StartsWithMagic", null, file)!);
    }
}
