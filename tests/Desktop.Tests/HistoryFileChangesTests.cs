using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Reading the steps of a history file: a file with no steps gives none, and the steps' own layout is read by the
/// round trip tests of <see cref="HistoryFile"/>.
/// </summary>
public sealed class HistoryFileChangesTests
{
    [Fact]
    public void AFileWithNoStepsReadsNoChanges()
    {
        using var reader = new BinaryReader(new MemoryStream());
        var (changes, points, thingIds) = HistoryFile.ReadChanges(reader, 0, 0, []);
        Assert.Empty(changes);
        Assert.Empty(points);
        Assert.Empty(thingIds);
    }
}
