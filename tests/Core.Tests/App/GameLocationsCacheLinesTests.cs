using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// GameLocations' cache file read in three parts: the first two lines and the rest (which may hold line breaks), or nothing
/// when the file is missing or has fewer than three parts.
/// </summary>
public class GameLocationsCacheLinesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-cache-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>Makes the test's folder.</summary>
    public GameLocationsCacheLinesTests() => Directory.CreateDirectory(_dir);

    /// <summary>Deletes the test's folder.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Directory.Delete(_dir, true);
    }

    private string PartsOf(string text)
    {
        string file = Path.Combine(_dir, "cache.txt");
        File.WriteAllText(file, text);
        return PrivateMember.Call<string[]>(typeof(GameLocations), "CacheLines", null, file) is { } parts ? string.Join("|", parts) : "null";
    }

    [Fact]
    public void AFileWithThreePartsGivesThemWithTheRestKeptWhole()
    {
        Assert.Equal("log|order|stamp\nrules", PartsOf("log\norder\nstamp\nrules"));
    }

    [Fact]
    public void AFileWithFewerThanThreePartsIsNotAUsableCache()
    {
        Assert.Equal("null", PartsOf("log\norder"));
    }

    [Fact]
    public void AMissingFileIsNotAUsableCache()
    {
        string missing = Path.Combine(_dir, "none.txt");

        Assert.Null(PrivateMember.Call<string[]>(typeof(GameLocations), "CacheLines", null, missing));
    }
}
