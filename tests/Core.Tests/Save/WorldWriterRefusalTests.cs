using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The checks made before a save plans anything: each refusal is given with its reason, and a valid request goes on.
/// </summary>
public class WorldWriterRefusalTests
{
    /// <summary>A world with no database, so zone resets cannot be made.</summary>
    private static WorldSave NoDatabase() => new() { Directory = "none", SaveNumber = 1 };

    [Fact]
    public void ResetsNeedTheWorldsDatabase()
    {
        var skipped = new List<string>();

        var refused = WorldWriter.Refusal(NoDatabase(), [], [], [], [new ZoneReset(0, 0, false, false)], new WorldWriter.Options(), skipped);

        Assert.NotNull(refused);
        Assert.Equal(Said.Of("save.writer.noDb").Shown, refused!.Message);
        Assert.False(refused.Saved);
    }

    [Fact]
    public void ANothingToDoIsRefused()
    {
        var refused = WorldWriter.Refusal(NoDatabase(), [], [], [], [], new WorldWriter.Options(), []);

        Assert.NotNull(refused);
        Assert.Equal(Said.Of("save.writer.nothing").Shown, refused!.Message);
    }

    [Fact]
    public void AnEditWithNoDatabaseNeedGoesOn()
    {
        var edit = new ZoneEdit(0, 0);

        var refused = WorldWriter.Refusal(NoDatabase(), [edit], [], [], [], new WorldWriter.Options(), []);

        Assert.Null(refused);
    }

    [Fact]
    public void ADeletionAloneIsEnoughToGoOn()
    {
        var refused = WorldWriter.Refusal(NoDatabase(), [], [0], [], [], new WorldWriter.Options(), []);

        Assert.Null(refused);
    }
}
