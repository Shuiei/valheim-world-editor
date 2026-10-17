using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The checks the ground and the masks tools make (ClaudeTools.GroundBox, Mask, Nature, Paths, Stamps, Heightmap, Ground):
/// what lies outside the area, a mask's biomes and paint, the kinds regrow_nature takes, a line's points, a brush's
/// settings, the answer shape_ground gives, and the stamp and heightmap pictures it reads.
/// </summary>
[Collection("World files")]
public class ClaudeToolsGroundHelpersTests
{
    // OutsideArea refuses a rectangle only when none of it is in the area.
    [AvaloniaFact]
    public async Task ARectangleTouchingTheAreaIsInsideAndOneBeyondIsRefused()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        var (minX, minZ, maxX, maxZ) = ClaudeTools.Bounds(scene);
        Assert.Null(ClaudeTools.OutsideArea(scene, minX, minZ, maxX, maxZ));
        Assert.Null(ClaudeTools.OutsideArea(scene, maxX - 1, maxZ - 1, maxX + 50, maxZ + 50));
        Assert.Equal($"The rectangle is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).", ClaudeTools.OutsideArea(scene, maxX + 10, minZ, maxX + 20, maxZ));
    }

    // LineOutside names the first point of a line that lies outside the area; a line inside it is none.
    [AvaloniaFact]
    public async Task ALineIsOutsideAtItsFirstPointBeyondTheArea()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        var scene = r.W.Session!.Scene;
        var (minX, minZ, maxX, maxZ) = ClaudeTools.Bounds(scene);
        float[][] inside = [[minX, minZ], [maxX, maxZ]];
        Assert.Null(ClaudeTools.LineOutside(scene, inside));
        float[][] beyond = [[minX, minZ], [maxX + 5, minZ]];
        Assert.Equal($"({maxX + 5}, {minZ}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}).", ClaudeTools.LineOutside(scene, beyond));
    }

    // LinePointsProblem: a line needs two points, each with an x and a z.
    [Fact]
    public void ALineNeedsTwoPointsWithXAndZ()
    {
        const string Problem = "A line needs at least 2 points, each [x, z].";
        Assert.Equal(Problem, ClaudeTools.LinePointsProblem([[1f, 2f]]));
        Assert.Equal(Problem, ClaudeTools.LinePointsProblem([[1f], [2f, 3f]]));
        Assert.Null(ClaudeTools.LinePointsProblem([[1f, 2f], [3f, 4f]]));
    }

    // KindsOf: all four kinds when none is named; a kind that is not one of them is named as the problem.
    [Fact]
    public void RegrowKindsDefaultToAllFourAndRefuseOthers()
    {
        var (all, noProblem) = ClaudeTools.KindsOf(null);
        Assert.Null(noProblem);
        Assert.Equal(4, all!.Count);
        var (rocks, rocksProblem) = ClaudeTools.KindsOf(["Rocks"]);
        Assert.Null(rocksProblem);
        Assert.Single(rocks!);
        var (none, badProblem) = ClaudeTools.KindsOf(["Trees", "Mushrooms"]);
        Assert.Null(none);
        Assert.Equal("Mushrooms: the kinds are Trees, Rocks, Bushes and Pickables.", badProblem);
    }

    // BiomesIn and PaintOf set a mask's biomes (names without spaces) and paint rule, and say which name is none.
    [Fact]
    public void AMaskTakesItsBiomesAndPaintByName()
    {
        var mask = new Mask();
        Assert.Null(ClaudeTools.BiomesIn(mask, null));
        Assert.Empty(mask.Biomes);
        Assert.Null(ClaudeTools.BiomesIn(mask, ["BlackForest"]));
        Assert.Single(mask.Biomes);
        Assert.StartsWith("Atlantis: the biomes are ", ClaudeTools.BiomesIn(mask, ["Atlantis"]));
        Assert.Null(ClaudeTools.PaintOf(mask, null));
        Assert.Equal(Mask.PaintRule.Any, mask.Paint);
        Assert.Null(ClaudeTools.PaintOf(mask, "dirt"));
        Assert.Equal(Mask.PaintRule.Dirt, mask.Paint);
        Assert.Equal("The mask's paint is any, unpainted, painted, dirt, cultivated or paved.", ClaudeTools.PaintOf(mask, "wet"));
        Assert.Equal(Mask.PaintRule.Dirt, mask.Paint);
    }

    // SmoothBrush holds each setting to its range.
    [Fact]
    public void ASmoothBrushHoldsItsSettingsToTheirRanges()
    {
        var high = ClaudeTools.SmoothBrush(50, 1, 90, 7);
        Assert.Equal(20f, high.NoiseAmp);
        Assert.Equal(2f, high.NoiseSize);
        Assert.Equal(80f, high.RestAngle);
        var low = ClaudeTools.SmoothBrush(-1, 300, 0, 7);
        Assert.Equal(0f, low.NoiseAmp);
        Assert.Equal(200f, low.NoiseSize);
        Assert.Equal(5f, low.RestAngle);
    }

    // GroundAnswer: nothing changed says so; a change gives its count, and the limit only when it was reached.
    [AvaloniaFact]
    public async Task AGroundAnswerSaysWhatChangedAndWhetherTheLimitWasReached()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        Assert.Equal("Nothing changed.", r.T.GroundAnswer(AreaTool.GroundAction.Flatten, 0, false));
        string plain = r.T.GroundAnswer(AreaTool.GroundAction.Flatten, 5, false);
        Assert.Contains("5 ground point(s) changed.", plain);
        Assert.DoesNotContain("±8 m limit", plain);
        Assert.Contains("Part of it reached the game's ±8 m limit.", r.T.GroundAnswer(AreaTool.GroundAction.Flatten, 5, true));
    }

    // StampDataOf: a stamp is named by its name or label; a name that is none says what there is.
    [Fact]
    public void AStampIsNamedOrSaidToBeNone()
    {
        var (data, problem) = ClaudeTools.StampDataOf("no-such-stamp");
        Assert.Null(data);
        Assert.StartsWith("No stamp \"no-such-stamp\": ", problem);
    }

    // PictureOf: a picture that cannot be read is said so, with why; no heightmap is given.
    [Fact]
    public void AHeightmapPictureThatCannotBeReadIsSaidSo()
    {
        string missing = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".png");
        var (picture, problem) = ClaudeTools.PictureOf(missing, 8, 8);
        Assert.Null(picture);
        Assert.StartsWith("That picture cannot be used: ", problem);
    }
}
