using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A copy's outline point coordinate: a number counts; a text, a missing value or an object does not.
/// </summary>
public class CopyFormatCoordinateTests
{
    /// <summary>
    /// Whether a node is an outline coordinate, through the copy format's own check.
    /// </summary>
    private static bool IsCoordinate(JsonNode? node) =>
        (bool)typeof(CopyFormat).GetMethod("IsCoordinate", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [node])!;

    [Fact]
    public void AFiniteNumberIsACoordinate()
    {
        Assert.True(IsCoordinate(JsonValue.Create(1.5)));
    }

    [Fact]
    public void AMissingValueIsNotACoordinate()
    {
        Assert.False(IsCoordinate(null));
    }

    [Fact]
    public void ATextIsNotACoordinate()
    {
        Assert.False(IsCoordinate(JsonValue.Create("1.5")));
    }

    [Fact]
    public void AnObjectIsNotACoordinate()
    {
        Assert.False(IsCoordinate(new JsonObject()));
    }

    [Fact]
    public void AWholeNumberIsACoordinate()
    {
        Assert.True(IsCoordinate(JsonValue.Create(3)));
    }
}
