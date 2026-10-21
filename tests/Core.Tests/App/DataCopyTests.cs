using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>An object a mod's tool adds or changes (DataCopy): each value set into its data, and taken away with null.</summary>
public class DataCopyTests
{
    [Fact]
    public void ValuesAreSetAndTakenAway()
    {
        var z = new ZdoData { Prefab = StableHash.Of("thing") };
        var c = new DataCopy("thing", new Vector3(1, 2, 3), Vector3.Zero, z) { Id = 4 };
        c.SetInt("i", 7);
        c.SetLong("l", 8);
        c.SetFloat("f", 1.5f);
        c.SetVector3("v", Vector3.One);
        c.SetString("s", "x");
        c.SetBytes("b", [1, 2]);
        var back = ZdoData.Parse(z.Serialize());
        var view = new ObjectView("thing", default, default, back);
        Assert.True(view.TryGetInt("i", out int i) && i == 7);
        Assert.True(view.TryGetLong("l", out long l) && l == 8);
        Assert.True(view.TryGetFloat("f", out float f) && f == 1.5f);
        Assert.True(view.TryGetVector3("v", out var v) && v == Vector3.One);
        Assert.True(view.TryGetString("s", out string? s) && s == "x");
        Assert.True(view.TryGetBytes("b", out byte[] b) && b.SequenceEqual(new byte[] { 1, 2 }));
        foreach (var unset in new Action[] { () => c.SetInt("i", null), () => c.SetLong("l", null), () => c.SetFloat("f", null), () => c.SetVector3("v", null), () => c.SetString("s", null), () => c.SetBytes("b", null) })
        {
            unset();
        }
        var empty = new ObjectView("thing", default, default, ZdoData.Parse(z.Serialize()));
        Assert.False(empty.TryGetInt("i", out _) || empty.TryGetLong("l", out _) || empty.TryGetFloat("f", out _) || empty.TryGetVector3("v", out _) || empty.TryGetString("s", out _) || empty.TryGetBytes("b", out _));
        Assert.Equal(4, c.Id);
        Assert.Same(z, c.Data);
    }
}
