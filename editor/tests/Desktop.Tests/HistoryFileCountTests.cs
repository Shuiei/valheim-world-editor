using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The counts read from a history file: a count is never negative, and a list starts with at most 4096 slots whatever
/// the file says, so a damaged count is not allocated up front.
/// </summary>
public class HistoryFileCountTests
{
    [Fact]
    public void ACountSmallerThanTheCapIsItsOwnCapacity()
    {
        Assert.Equal(10, PrivateMember.Call<int>(typeof(HistoryFile), "Capacity", null, 10));
    }

    [Fact]
    public void ACountPastTheCapGivesTheCap()
    {
        Assert.Equal(4096, PrivateMember.Call<int>(typeof(HistoryFile), "Capacity", null, 100000));
    }

    [Fact]
    public void ACountInTheFileIsReadAsWritten()
    {
        using var reader = new BinaryReader(new MemoryStream([5, 0, 0, 0]));

        Assert.Equal(5, PrivateMember.Call<int>(typeof(HistoryFile), "ReadCount", null, reader));
    }

    [Fact]
    public void ANegativeCountInTheFileIsRefused()
    {
        using var reader = new BinaryReader(new MemoryStream([0xFF, 0xFF, 0xFF, 0xFF]));

        Assert.Throws<InvalidDataException>(() => PrivateMember.Call(typeof(HistoryFile), "ReadCount", null, reader));
    }
}
