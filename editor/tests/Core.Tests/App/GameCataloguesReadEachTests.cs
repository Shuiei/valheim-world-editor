using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading the mods' bundles one by one: a file that is no bundle, or a damaged one, is left out (said in the log), and
/// the reading goes on, so one bad mod does not keep the others' pieces from being listed.
/// </summary>
public sealed class GameCataloguesReadEachTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-readeach-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Deletes the folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void ABrokenBundleIsLeftOutAndTheReadingGoesOn()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "a.not-a-bundle"), "hello");
        // A bundle's first bytes, then nothing a bundle holds.
        File.WriteAllBytes(Path.Combine(_dir, "b.broken"), [.. "UnityFS\0"u8.ToArray(), .. new byte[64], .. Enumerable.Repeat((byte)0xff, 256)]);
        var progress = new List<(int, int)>();

        var read = GameCatalogues.ReadEach(_dir, ModScan.Kinds, [], (d, n) => progress.Add((d, n)));

        Assert.Empty(read);
        Assert.Equal([(0, 2), (1, 2)], progress);
    }
}
