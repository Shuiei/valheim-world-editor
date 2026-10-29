using Xunit;
using static TerrainEditor.Desktop.Tests.ScriptHostRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Script tool's limits: endless loops and huge shapes stopped, line numbers and directives kept through loops, a
/// step that cannot move refused, and the counted offsets within a span.
/// </summary>
public class ScriptHostLimitTests
{
    // A loop of the script's own that calls nothing of the API's is still ended by Stop and the 2-minute limit.
    [Theory]
    [InlineData("while (true) { }")]
    [InlineData("int n = 0;\nfor (;;) n++;")]
    [InlineData("do { } while (true);")]
    [InlineData("foreach (int i in Enumerable.Repeat(0, int.MaxValue)) { }")]
    [InlineData("again: goto again;")]
    [InlineData("static IEnumerable<int> Forever() { while (true) yield return 0; }\nforeach (int i in Forever()) { }")]
    public async Task AScriptsOwnEndlessLoopIsStopped(string code)
    {
        var s = TestScenes.Flat(1);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var run = Task.Run(() => Run(s, code, cancel.Token), TestContext.Current.CancellationToken);
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    // The checks put into loops hold no line break: an error after a loop still says the script's own line.
    [Fact]
    public void LoopsKeepTheScriptsLineNumbers()
    {
        var s = TestScenes.Flat(1);
        var ex = Assert.Throws<ArgumentException>(() => Run(s, "for (int i = 0; i < 2; i++)\n    Print(i);\nwhile (false)\n{\n}\nObjects.Place(\"NoSuchThing\", 10, 10);\n"));
        Assert.Equal("line 6: ", ScriptHost.Where(ex));
        var (image, _, errors) = ScriptHost.Compile("for (int i = 0; i < 2; i++)\n{\n}\nGround.Raise(1, 2);\n");
        Assert.Null(image);
        Assert.StartsWith("line 4: ", Assert.Single(errors));
    }

    // The checks put into loops keep #if and the other directives at the start of their own lines: next to a loop's
    // body, before it or after it, the script still compiles.
    [Theory]
    [InlineData("while (true)\n#if true\n    break;\n#endif\nPrint(1);\n")]
    [InlineData("while (false) { }\n#if true\nPrint(1);\n#endif\n")]
    [InlineData("int n = 0;\nagain: n++;\n#if true\nif (n < 3) goto again;\n#endif\nPrint(n);\n")]
    public void LoopsNextToDirectivesStillCompile(string code) => Assert.Empty(ScriptHost.Compile(code).Errors);

    // A step that cannot move along the area would give the same point for ever: it is refused.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1e-7f")]
    [InlineData("float.NaN")]
    public void APointsStepThatCannotMoveIsRefused(string step)
    {
        var s = TestScenes.Flat(1);
        var ex = Assert.Throws<ArgumentException>(() => Run(s, $"Print(Area.Points({step}).Count());"));
        Assert.Contains("give one above 0", ex.Message);
    }

    // Counted steps give the points a step that adds up exactly always gave.
    [Fact]
    public void PointsAreCountedInSteps()
    {
        var s = TestScenes.Flat(1);
        Assert.Equal("16", Run(s, "Print(Area.Points(16).Count());").Output.ToString().Trim());
        Assert.Equal("15876", Run(s, "Print(Area.Points(0.5f).Count());").Output.ToString().Trim());
    }

    // A shape or mountain far larger than the area ends, having changed only the area (a float counter past 2^24 m
    // stopped growing and never ended).
    [Theory]
    [InlineData("Ground.Shape(0, 0, 1e9f, (dx, dz) => 1);")]
    [InlineData("Ground.Mountain(0, 0, height: 10, radius: 1e9f);")]
    public async Task AHugeShapeEnds(string code)
    {
        var s = TestScenes.Flat(1);
        var run = Task.Run(() => Run(s, code), TestContext.Current.CancellationToken);
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)));
        Assert.NotEmpty((await run).Heights);
    }

    // The whole-metre offsets within reach that can land in a span: short however large the reach, kept up to 2^24 m,
    // none when there is nothing to count.
    [Fact]
    public void OffsetsAreCountedOnlyWhereTheyCanLand()
    {
        Assert.Equal((-4, 3), ScriptHost.Offsets(0, 3.5f, -100, 100));
        Assert.Equal((-11, 10), ScriptHost.Offsets(0.5f, 1e9f, -10, 10));
        Assert.Equal((-11, 10), ScriptHost.Offsets(0.5f, float.PositiveInfinity, -10, 10));
        Assert.Equal((-5, 2), ScriptHost.Offsets(0, 5, -100, 2));
        // Far off: the offsets that land and can be counted (up to 2^24 m) are kept.
        Assert.Equal((-16777216, -16777154), ScriptHost.Offsets(16777218f, 16777218f, 0, 64));
        foreach (var (c, reach) in new[] { (0f, -1f), (0f, float.NaN), (float.NaN, 5f), (1e9f, 5f), (-1e30f, 1e31f) })
        {
            var (from, to) = ScriptHost.Offsets(c, reach, -10, 10);
            Assert.True(from > to, $"{c}, {reach}: {from}..{to}");
        }
    }
}
