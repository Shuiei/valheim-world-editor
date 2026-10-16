using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Blueprint files of other mods at their edges (old comma decimals, other mods' sections, pieces seen from straight
/// above, terrain only, square and round terrain with paint), and "Regrow nature" over zones of every biome of the test
/// world's map: always the same draws, inside the zones, on the ground (or the water), Unity's random state put back,
/// and Unity's LookRotation in each of its cases.
/// </summary>
public class BlueprintFormatsEdgeTests
{
    [Fact]
    public void OldPlanBuildFilesWithCommaDecimalsAreRead()
    {
        var bp = BlueprintFormats.Parse("old.blueprint", "#Pieces\nwood_floor;Building;1,5;0,25;-2,5;0;0;0;1;;1;1;1\n");
        var p = Assert.Single(bp.Pieces);
        Assert.Equal(new Vector3(1.5f, 0.25f, -2.5f), p.Position);
        var v = BlueprintFormats.Parse("old.vbuild", "woodwall 0 0 0 1 1,5 0 -2,25\n");
        Assert.Equal(new Vector3(1.5f, 0, -2.25f), Assert.Single(v.Pieces).Position);
    }

    [Fact]
    public void OtherModsSectionsAreSkippedUntilAKnownOne()
    {
        var bp = BlueprintFormats.Parse("mixed.blueprint", string.Join("\n",
            "#Name:", "#TerrainHeight", "1;2;3", "this is not a piece", "#Pieces", "wood_floor;Building;0;0;0;0;0;0;1", "﻿", "  "));
        // An empty #Name keeps the file's name.
        Assert.Equal("mixed", bp.Name);
        Assert.Single(bp.Pieces);
        Assert.Equal(0, bp.SkippedLines);
    }

    [Theory]
    [InlineData(90f)]
    [InlineData(-90f)]
    public void APieceSeenFromStraightAboveKeepsItsTurn(float pitch)
    {
        // Pitched a quarter turn and yawed 30°: only yaw and roll together are defined; the angles found
        // rebuild the same rotation.
        var q = BlueprintFormats.FromEuler(new Vector3(pitch, 30, 0));
        var e = BlueprintFormats.ToEuler(q);
        Assert.Equal((pitch + 360) % 360, e.X, 1);
        Assert.Equal(0f, e.Z, 3);
        var back = BlueprintFormats.FromEuler(e);
        Assert.True(MathF.Abs(Quaternion.Dot(q, back)) > 0.9999f, $"{e}");
    }

    [Fact]
    public void ABlueprintOfGroundOnlyIsLevelledAndPainted()
    {
        var bp = BlueprintFormats.Parse("ground.blueprint", string.Join("\n",
            "#Terrain", "square;0;10;0;2;45;0;paved", "circle;6;12;0;1.5;0;0;Cultivated", "circle;-6;11;0;1;0;0;dirt", "circle;0;9;8;1;0;0;grass"));
        var clip = BlueprintFormats.ToClip(bp, _ => true, out var unknown);
        Assert.Empty(unknown);
        Assert.Empty((JsonArray)clip["objects"]!);
        int w = (int)clip["w"]!, h = (int)clip["h"]!;
        int[] rel = [.. ((JsonArray)clip["rel"]!).Select(v => (int)v!)];
        int[] pnt = [.. ((JsonArray)clip["pnt"]!).Select(v => (int)v!)];
        Assert.True(w > 10 && h > 8);
        // Heights in cm above the lowest entry (9 m); points no entry covers have no ground.
        Assert.Contains(100, rel);
        Assert.Contains(300, rel);
        Assert.Contains(200, rel);
        Assert.Contains(0, rel);
        Assert.Contains(-32768, rel);
        // Paint: paved (blue), cultivated (green), dirt (red); "grass" is not a paint and leaves it alone.
        var paints = Enumerable.Range(0, w * h).Where(i => rel[i] != -32768).Select(i => (pnt[i * 4], pnt[i * 4 + 1], pnt[i * 4 + 2])).Distinct().ToList();
        Assert.Contains((0, 0, 255), paints);
        Assert.Contains((0, 255, 0), paints);
        Assert.Contains((255, 0, 0), paints);
        Assert.Contains((-255, -255, -255), paints);
    }

    [Fact]
    public void TheGroundGridReachesOnlyAsFarAsTheTerrainMarks()
    {
        // Pieces 100 m apart with one small mark near one of them: the copy is centred on the mark, its grid spans
        // the mark only, and the pieces are placed from there, however far they are.
        var bp = BlueprintFormats.Parse("far.blueprint", string.Join("\n",
            "#Terrain", "circle;20;0;0;1;0;0;dirt", "#Pieces", "wood_wall;0;0;0;0;0;0;0;1;", "wood_wall;0;3000;0;0;0;0;0;1;"));
        var clip = BlueprintFormats.ToClip(bp, _ => true, out _);
        Assert.Equal((3, 3), ((int)clip["w"]!, (int)clip["h"]!));
        Assert.Equal(0, (int)((JsonArray)clip["rel"]!)[4]!);
        var objects = (JsonArray)clip["objects"]!;
        Assert.Equal([-20f, 2980f], objects.Select(o => (float)o!["dx"]!));
        // The outline still holds everything.
        Assert.Equal(-21f, (float)((JsonArray)clip["poly"]!)[0]!["gx"]!);
        Assert.Equal(2981f, (float)((JsonArray)clip["poly"]!)[1]!["gx"]!);
    }

    [Fact]
    public void TerrainMarksTooFarApartAreRefused()
    {
        var bp = BlueprintFormats.Parse("wide.blueprint", string.Join("\n", "#Terrain", "circle;0;0;0;1;0;0;dirt", "circle;2000;0;0;1;0;0;dirt"));
        var e = Assert.Throws<InvalidDataException>(() => BlueprintFormats.ToClip(bp, _ => true, out _));
        Assert.Contains("ground marks span 2002 m by 2 m", e.Message);
        // A radius that would overflow the grid is refused the same way, not an overflow.
        bp = BlueprintFormats.Parse("huge.blueprint", string.Join("\n", "#Terrain", "circle;0;0;0;100000;0;0;dirt"));
        Assert.Throws<InvalidDataException>(() => BlueprintFormats.ToClip(bp, _ => true, out _));
    }

    [Fact]
    public void ANumberThatIsNotFiniteMakesALineUnreadable()
    {
        var bp = BlueprintFormats.Parse("nan.blueprint", string.Join("\n",
            "#Terrain", "circle;NaN;0;0;1;0;0;dirt", "circle;0;0;0;Infinity;0;0;dirt", "#Pieces", "wood_wall;0;-Infinity;0;0;0;0;0;1;", "wood_wall;0;1;0;0;0;0;0;1;"));
        Assert.Equal(3, bp.SkippedLines);
        Assert.Empty(bp.Terrain);
        Assert.Single(bp.Pieces);
        // A negative radius too: it would turn the mark's extents inside out.
        bp = BlueprintFormats.Parse("neg.blueprint", string.Join("\n", "#Terrain", "circle;0;0;0;-10000;0;0;dirt"));
        Assert.Equal(1, bp.SkippedLines);
        Assert.Empty(bp.Terrain);
    }

    [Fact]
    public void AnEmptyBlueprintIsAOnePointCopy()
    {
        var clip = BlueprintFormats.ToClip(BlueprintFormats.Parse("empty.vbuild", ""), _ => true, out _);
        Assert.Equal((1, 1), ((int)clip["w"]!, (int)clip["h"]!));
        Assert.Equal(-32768, (int)((JsonArray)clip["rel"]!)[0]!);
        Assert.Equal(4, ((JsonArray)clip["poly"]!).Count);
    }

    [Fact]
    public void WritingAnEmptyCopyGivesOnlyTheHeader()
    {
        var clip = new JsonObject { ["objects"] = new JsonArray() };
        Assert.Equal("", BlueprintFormats.Write(clip, "vbuild", "x", _ => 0));
        string planbuild = BlueprintFormats.Write([], "blueprint", "Nothing", _ => 99);
        Assert.Contains("#Name:Nothing", planbuild);
        Assert.EndsWith("#Pieces\n", planbuild);
    }
}
