using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Place tool's memory file when it is not what it should be: empty JSON, broken JSON (kept aside), nulls, and a
/// folder that cannot be made. Alone (DataDir): it points the memory at a file of its own.
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
    public void NullMembersAreTakenAsEmpty()
    {
        // A hand-edited file's nulls crashed the Place panel (Chosen, Weights) and loading a preset (its Kinds).
        string file = Path.Combine(_dir, "place.json");
        PlaceMemory.PathOverride = file;
        File.WriteAllText(file, """
            {"Presets": [null, {"Name": "no kinds", "Kinds": null}, {"Name": null, "Kinds": {"Oak1": 1}}, {"Name": "ok", "Kinds": {"Oak1": 2}}],
             "Favourites": null, "Recent": [null, "Oak1"], "Chosen": null, "Weights": null, "Builders": null}
            """);
        var m = PlaceMemory.Load();
        Assert.Equal("ok", Assert.Single(m.Presets).Name);
        Assert.Empty(m.Favourites);
        Assert.Equal(["Oak1"], m.Recent);
        Assert.Empty(m.Chosen);
        Assert.Empty(m.Weights);
        Assert.Empty(m.Builders);
    }

    [Fact]
    public void ABrokenFileIsKeptAsideBeforeItIsWrittenOver()
    {
        // A broken file gave an empty memory that the next save wrote over: presets, favourites and weights lost. It is
        // copied aside as place.json.bad first; when that cannot be done, nothing is written over it.
        string file = Path.Combine(_dir, "place.json");
        PlaceMemory.PathOverride = file;
        File.WriteAllText(file, "{ not json");
        Directory.CreateDirectory(file + ".bad");
        PlaceMemory.Load().ToggleFavourite("Oak1");
        Assert.Equal("{ not json", File.ReadAllText(file));
        Directory.Delete(file + ".bad");
        PlaceMemory.Load().ToggleFavourite("Oak1");
        Assert.Equal("{ not json", File.ReadAllText(file + ".bad"));
        Assert.Equal("Oak1", Assert.Single(PlaceMemory.Load().Favourites));
        // Written through a temporary file, none left behind.
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp"));
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
