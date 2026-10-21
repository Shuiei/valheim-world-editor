using System.Numerics;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>The shared stand-in for the editor a tool sees: it keeps every commit in order and the last preview, status and texts.</summary>
public class ToolContextRecorderTests
{
    private sealed class Recorder() : ToolContextRecorder(new PlainArea(-8, -8, 8, 8, 40))
    {
        public override float Number(string id) => 1;
    }

    [Fact]
    public void KeepsWhatTheToolDid()
    {
        var ctx = new Recorder();
        Assert.Equal(40, ctx.Area.GroundHeight(0, 0));
        Assert.Equal(0, ctx.Choice("any"));
        Assert.False(ctx.IsOn("any"));
        var first = new ToolEdit().Add("a", Vector3.Zero, Vector3.Zero);
        var second = new ToolEdit();
        ctx.Commit("one", first);
        ctx.Commit("two", second);
        Assert.Equal([("one", first), ("two", second)], ctx.Commits);
        ctx.Status("first");
        ctx.Status("last");
        Assert.Equal("last", ctx.Said);
        ctx.SetText("hint", "a");
        ctx.SetText("hint", "b");
        Assert.Equal("b", Assert.Single(ctx.Texts).Value);
        var shape = new Shape([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2], Vector4.One);
        ctx.Preview([shape]);
        Assert.Same(shape, Assert.Single(ctx.Shown));
        ctx.Preview([]);
        Assert.Empty(ctx.Shown);
    }
}
