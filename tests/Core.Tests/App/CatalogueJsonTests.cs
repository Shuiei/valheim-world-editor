using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game data's JSON written as Python's json module wrote the catalogues: floats as repr prints them, round
/// half to even at the exact value, "%.9G", the separators, indents, sorted keys and ASCII escapes. The expected
/// texts are Python 3's own output for the same values.
/// </summary>
public class CatalogueJsonTests
{
    [Theory]
    [InlineData(1.0, "1.0")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.30000001192092896, "0.30000001192092896")]
    [InlineData(1e-05, "1e-05")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.000123, "0.000123")]
    [InlineData(1.5e-7, "1.5e-07")]
    [InlineData(123456789012345678.0, "1.2345678901234568e+17")]
    [InlineData(1e16, "1e+16")]
    [InlineData(9999999999999998.0, "9999999999999998.0")]
    [InlineData(1e22, "1e+22")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(100.0, "100.0")]
    [InlineData(3.14159, "3.14159")]
    public void FloatsAreWrittenAsPythonsRepr(double value, string python) => Assert.Equal(python, CatalogueJson.Repr(value));

    [Fact]
    public void ZeroNaNAndInfinitiesAreWrittenAsPythonWritesThem()
    {
        Assert.Equal("0.0", CatalogueJson.Repr(0.0));
        Assert.Equal("-0.0", CatalogueJson.Repr(-0.0));
        Assert.Equal("NaN", CatalogueJson.Repr(double.NaN));
        Assert.Equal("Infinity", CatalogueJson.Repr(double.PositiveInfinity));
        Assert.Equal("-Infinity", CatalogueJson.Repr(double.NegativeInfinity));
    }

    [Theory]
    [InlineData(2.5, 0, 2.0)]
    [InlineData(3.5, 0, 4.0)]
    [InlineData(0.125, 2, 0.12)]
    [InlineData(0.375, 2, 0.38)]
    // The exact values below are just under ...5: Python rounds them down, not up.
    [InlineData(-1.005, 2, -1.0)]
    [InlineData(2.675, 2, 2.67)]
    [InlineData(123.456, 1, 123.5)]
    // Whole numbers already, and the smallest subnormal at 3 decimals.
    [InlineData(1e20, 2, 1e20)]
    [InlineData(5e-324, 3, 0.0)]
    // More decimals than exact powers of ten: read back from text.
    [InlineData(0.5, 30, 0.5)]
    public void RoundIsPythonsHalfToEvenAtTheExactValue(double x, int n, double python) =>
        Assert.Equal(python, CatalogueJson.Round(x, n));

    [Fact]
    public void RoundKeepsZeroesSignsAndWhatIsNotFinite()
    {
        Assert.True(double.IsNegative(CatalogueJson.Round(-0.0, 3)));
        Assert.True(double.IsNegative(CatalogueJson.Round(-0.0001, 2)));
        Assert.True(double.IsNaN(CatalogueJson.Round(double.NaN, 2)));
        Assert.Equal(double.PositiveInfinity, CatalogueJson.Round(double.PositiveInfinity, 2));
    }

    [Theory]
    [InlineData(1.23456789123, 1.23456789)]
    [InlineData(123456789.123, 123456789.0)]
    [InlineData(1234567891234.5, 1234567890000.0)]
    [InlineData(-0.000123456789123, -0.000123456789)]
    [InlineData(0.0, 0.0)]
    public void Significant9IsPythonsFloatOfPercent9G(double x, double python) => Assert.Equal(python, CatalogueJson.Significant9(x));

    [Fact]
    public void IndentedTextIsPythonsJsonDumps()
    {
        var inner = new CatalogueJson.Dict { ["z"] = null, ["a"] = true };
        var root = new CatalogueJson.Dict { ["b"] = new List<object?> { 1, 2.0, inner }, ["a"] = "é\n\t\"\\\b\f\r\u0001~" };
        string python = """
            {
              "b": [
                1,
                2.0,
                {
                  "z": null,
                  "a": true
                }
              ],
              "a": "é\n\t\"\\\b\f\r\u0001~"
            }
            """;
        Assert.Equal(python.ReplaceLineEndings("\n"), CatalogueJson.Write(root, new CatalogueJson.Style(ItemSep: ",", Indent: 2)));
    }

    [Fact]
    public void CompactSortedTextWithoutAsciiEscapesIsPythonsJsonDumps()
    {
        var inner = new CatalogueJson.Dict { ["z"] = null, ["a"] = false };
        var root = new CatalogueJson.Dict { ["b"] = new List<object?> { 1, 2.0, inner }, ["a"] = "é" };
        var style = new CatalogueJson.Style(ItemSep: ",", KeySep: ":", SortKeys: true, Ascii: false);
        Assert.Equal("{\"a\":\"é\",\"b\":[1,2.0,{\"a\":false,\"z\":null}]}", CatalogueJson.Write(root, style));
        // The default style is Python's: ", " and ": " on one line.
        Assert.Equal("[1, 9876543210, \"x\"]", CatalogueJson.Write(new List<object?> { 1, 9876543210L, "x" }, new CatalogueJson.Style()));
    }

    [Fact]
    public void EmptyContainersStayOnOneLineWithIndentZero()
    {
        var root = new CatalogueJson.Dict { ["x"] = new List<object?>(), ["y"] = new CatalogueJson.Dict() };
        Assert.Equal("{\n\"x\": [],\n\"y\": {}\n}", CatalogueJson.Write(root, new CatalogueJson.Style(ItemSep: ",", Indent: 0)));
    }

    [Fact]
    public void ADictKeepsItsKeysInTheOrderTheyCame()
    {
        var d = new CatalogueJson.Dict { ["b"] = 1, ["a"] = 2, ["c"] = 3 };
        // Set again: its place kept.
        d["b"] = 4;
        Assert.Equal(["b", "a", "c"], d.Select(kv => kv.Key));
        Assert.Equal(4, d["b"]);
        Assert.Equal(3, d.Count);
        Assert.True(d.ContainsKey("a"));
        Assert.True(d.TryGetValue("c", out object? c));
        Assert.Equal(3, c);
        Assert.False(d.TryGetValue("x", out _));
        d.Remove("a");
        // Removing what is not there does nothing.
        d.Remove("x");
        Assert.Equal(["b", "c"], d.Select(kv => kv.Key));
        Assert.Throws<KeyNotFoundException>(() => d["a"]);
        // The non-generic enumerator too.
        int n = 0;
        foreach (object _ in (System.Collections.IEnumerable)d)
        {
            n++;
        }
        Assert.Equal(2, n);
    }

    [Fact]
    public void AnythingElseThanJsonsKindsIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(() => CatalogueJson.Write(new List<object?> { 1.5f }, new CatalogueJson.Style()));
        Assert.Contains("Single", e.Message);
    }
}
