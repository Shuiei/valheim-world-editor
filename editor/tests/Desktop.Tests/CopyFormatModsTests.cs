using System.Text.Json.Nodes;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What mods' extensions keep with a copy, in the clipboard format ("mods": base64 text by copier name): written only
/// when there is some, read back as it was (and kept by a blueprint without builders), refused when not in that form.
/// </summary>
public class CopyFormatModsTests
{
    private static CopyData Empty() => new() { W = 1, H = 1, Rel = [float.NaN], Wt = [0f], Pnt = [-1f, -1, -1, -1], Objects = [], Poly = [] };

    [Fact]
    public void WhatModsKeepIsWrittenAndReadBack()
    {
        var c = Empty();
        Assert.False(CopyFormat.ToJson(c).ContainsKey("mods"));

        c.Kept = new Dictionary<string, byte[]> { ["Tunnels/rock"] = [1, 2, 255], ["Other/x"] = [0] };
        var json = CopyFormat.ToJson(c);
        Assert.Equal("AQL/", (string?)json["mods"]!["Tunnels/rock"]);
        var back = CopyFormat.FromJson(JsonNode.Parse(json.ToJsonString())!.AsObject());
        Assert.Equal([1, 2, 255], back.Kept["Tunnels/rock"]);
        Assert.Equal([0], back.Kept["Other/x"]);
        Assert.Same(c.Kept, c.WithoutBuilders().Kept);
    }

    [Theory]
    [InlineData("[]", "is not an object")]
    [InlineData("""{"a/b":1}""", "\"a/b\" is not a text")]
    [InlineData("""{"a/b":"not base64!"}""", "\"a/b\" is not base64")]
    public void ModsNotInTheirFormAreRefused(string mods, string why)
    {
        var json = CopyFormat.ToJson(Empty());
        json["mods"] = JsonNode.Parse(mods);
        Assert.Contains(why, Assert.Throws<FormatException>(() => CopyFormat.FromJson(json)).Message);
    }
}
