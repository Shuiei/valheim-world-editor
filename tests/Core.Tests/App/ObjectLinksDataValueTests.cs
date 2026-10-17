using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The values a link writes into an object's data (ObjectLinks.IntIn reads one, ObjectLinks.Vec writes one): an int is
/// read only from the ints section and only when it parses, and a vector is written in invariant, round-trip text.
/// </summary>
public class ObjectLinksDataValueTests
{
    private static int IntIn(List<ObjectField> data, int key) =>
        (int)PrivateCall.Static(typeof(ObjectLinks), "IntIn", data, key)!;

    private static string Vec(Vector3 v) =>
        (string)PrivateCall.Static(typeof(ObjectLinks), "Vec", v)!;

    [Fact]
    public void AnIntIsReadFromItsKeyInTheIntsSection()
    {
        var data = new List<ObjectField> { new("ints", 4, "12") };
        Assert.Equal(12, IntIn(data, 4));
    }

    [Fact]
    public void AKeyInAnotherSectionIsNotAnInt()
    {
        var data = new List<ObjectField> { new("floats", 4, "12") };
        Assert.Equal(0, IntIn(data, 4));
    }

    [Fact]
    public void AMissingOrEmptyIntIsZero()
    {
        var data = new List<ObjectField> { new("ints", 4, null) };
        Assert.Equal(0, IntIn(data, 4));
        Assert.Equal(0, IntIn(data, 9));
    }

    [Fact]
    public void AnIntThatDoesNotParseIsZero()
    {
        var data = new List<ObjectField> { new("ints", 4, "twelve") };
        Assert.Equal(0, IntIn(data, 4));
    }

    [Fact]
    public void AVectorIsWrittenWithItsComponentsInRoundTripText()
    {
        Assert.Equal("1.5 -2 0.1", Vec(new Vector3(1.5f, -2f, 0.1f)));
    }

    [Fact]
    public void AVectorWrittenAndReadBackIsTheSameVector()
    {
        Vector3 v = new(0.3f, 1234.5678f, -0.00012f);
        float[] parts = [.. Vec(v).Split(' ').Select(p => float.Parse(p, CultureInfo.InvariantCulture))];
        Assert.Equal(v, new Vector3(parts[0], parts[1], parts[2]));
    }
}
