using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Whether an object's data as it is now is the data the Inspector opened with: a missing object never is.
/// </summary>
public sealed class InspectorPanelSameBytesTests
{
    [Fact]
    public void AMissingObjectIsNotTheSameAsOpened()
    {
        Assert.False(InspectorPanel.SameBytes(null, [1, 2]));
    }

    [Fact]
    public void TheSameBytesAreTheSame()
    {
        Assert.True(InspectorPanel.SameBytes([1, 2, 3], [1, 2, 3]));
    }

    [Fact]
    public void ChangedBytesAreNotTheSame()
    {
        Assert.False(InspectorPanel.SameBytes([1, 2, 4], [1, 2, 3]));
    }
}
