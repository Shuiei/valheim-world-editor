using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The mod's models found beside the player's own copy of its plugin, read once and kept; without the mod, or for a
/// model it lacks or that cannot be read, none (told once in the log).
/// </summary>
public sealed class ModelLibraryTests : IDisposable
{
    private readonly ModFolder _mod = new();
    private readonly List<string> _log = [];

    public void Dispose() => _mod.Dispose();

    [Fact]
    public void AModelIsReadFromBesideThePluginOnce()
    {
        _mod.Put("track_straight", ModelBytes.Triangle("stone", Vector3.Zero));
        var host = new FakeHost { Plugin = _mod.Plugin };
        var models = new ModelLibrary(host, _log.Add);

        var first = models.Get("track_straight");
        var again = models.Get("track_straight");

        Assert.NotNull(first);
        Assert.Same(first, again);
        Assert.Equal("stone", Assert.Single(first.Parts).Slot);
        // The plugin is looked for once, not for every model drawn.
        Assert.Equal(1, host.Looks);
        Assert.Empty(_log);
    }

    [Fact]
    public void WithoutTheModNothingIsDrawn()
    {
        var models = new ModelLibrary(new FakeHost(), _log.Add);

        Assert.Null(models.Get("track_straight"));
        Assert.Empty(_log);
    }

    [Fact]
    public void AModelMissingOrDamagedIsToldOnce()
    {
        _mod.Put("wagon", [1, 2, 3]);
        var models = new ModelLibrary(new FakeHost { Plugin = _mod.Plugin }, _log.Add);

        Assert.Null(models.Get("locomotive"));
        Assert.Null(models.Get("locomotive"));
        Assert.Null(models.Get("wagon"));

        Assert.Equal(2, _log.Count);
        Assert.Contains("locomotive.bfm", _log[0], StringComparison.Ordinal);
        Assert.Contains("wagon.bfm", _log[1], StringComparison.Ordinal);
        Assert.Contains("InvalidDataException", _log[1], StringComparison.Ordinal);
    }
}
