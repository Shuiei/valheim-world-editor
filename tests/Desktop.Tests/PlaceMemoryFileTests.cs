using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's memory file when it is not what it should be: empty JSON, broken JSON, and a folder that cannot
/// be made. Alone (DataDir): it points the memory at a file of its own.
/// </summary>
[Collection("DataDir")]
public sealed class PlaceMemoryFileTests : IDisposable
{
    private readonly string? _was = PlaceMemory.PathOverride;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-placemem-" + Guid.NewGuid().ToString("N")[..8]);

    public PlaceMemoryFileTests() => Directory.CreateDirectory(_dir);

    /// <inheritdoc/>
    public void Dispose()
    {
        PlaceMemory.PathOverride = _was;
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ANullOrBrokenFileGivesAnEmptyMemory()
    {
        string file = Path.Combine(_dir, "place.json");
        PlaceMemory.PathOverride = file;
        File.WriteAllText(file, "null");
        Assert.Empty(PlaceMemory.Load().Presets);
        File.WriteAllText(file, "{ not json");
        var m = PlaceMemory.Load();
        Assert.Empty(m.Recent);
        Assert.Empty(m.Favourites);
    }

    [Fact]
    public void SavingWhereNoFolderCanBeMadeIsIgnored()
    {
        // A file stands where the folder would go.
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "");
        PlaceMemory.PathOverride = Path.Combine(blocker, "place.json");
        var m = new PlaceMemory();
        m.ToggleFavourite("Oak1");
        Assert.Equal("Oak1", Assert.Single(m.Favourites));
        Assert.False(File.Exists(PlaceMemory.PathOverride));
    }
}
