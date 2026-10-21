using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Reading the steps of a history file: a file with no steps gives none; a step of format 2 has its hidden objects
/// (mods' data holders) after its resets, one of format 1 none; the steps' own layout is read by the round trip tests of
/// <see cref="HistoryFile"/>.
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

    /// <summary>One step with no points, zones, things or resets, then (format 2) one hidden object, the file's object 1.</summary>
    private static BinaryReader Step(bool hidden)
    {
        var m = new MemoryStream();
        using (var w = new BinaryWriter(m, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write("Dig");
            w.Write(DateTime.Now.Ticks);
            w.Write(false);
            w.Write(false);
            w.Write(-1);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            if (hidden)
            {
                w.Write(1);
                w.Write(1);
                w.Write(true);
                w.Write(false);
            }
        }
        m.Position = 0;
        return new BinaryReader(m);
    }

    [Fact]
    public void AStepOfTheNewFormatHasItsHiddenObjects()
    {
        using var two = Step(hidden: true);
        var (changes, _, _) = HistoryFile.ReadChanges(two, 1, 0, [40, 41], version: 2);
        Assert.Equal([(41, true, false)], Assert.Single(changes).C.Hidden);
        using var one = Step(hidden: false);
        var (old, _, _) = HistoryFile.ReadChanges(one, 1, 0, [40, 41], version: 1);
        Assert.Empty(Assert.Single(old).C.Hidden);
    }
}
