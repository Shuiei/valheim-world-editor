using Xunit;
using static TerrainEditor.Desktop.Tests.AreaToolTestScene;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool's actions on the ground: what a selection changes (only the inside), the volume it reports, and restoring a mountain.
/// </summary>
public class AreaToolGroundTests
{
    [Fact]
    public void GroundActionsChangeOnlyTheInside()
    {
        var s = TestScenes.Flat(2);
        var a = Box(40, 40, 80, 80, soft: 2);
        Assert.Contains("dig 0 m³, fill", a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 33, 2));
        s.EditGround("Area: Flatten", g =>
        {
            var (t, rect, _) = a.Apply(g, s.Brush, AreaTool.GroundAction.Flatten, 33, 0, new float[4]);
            return (t, rect);
        });
        Assert.Equal(33, H(s, 60, 60), 3);
        Assert.Equal(30, H(s, 90, 60), 3);
        Assert.Equal(33, a.Average(s.Ground)!.Value, 0);
        Assert.Contains("raised", a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 2));
        RestoreArea(s, a);
        Assert.Equal(30, H(s, 60, 60), 3);
        Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.OrderBy(z => z), a.ZonesUnder(s.Ground).OrderBy(z => z));
    }

    // An action of a kind the tool does not know (none the panel offers) leaves the ground as it was, and nothing fails.
    [Theory]
    [InlineData((AreaTool.GroundAction)(-1))]
    [InlineData((AreaTool.GroundAction)99)]
    public void AnActionOfNoKnownKindLeavesTheGroundAsItWas(AreaTool.GroundAction act)
    {
        var s = TestScenes.Flat(2);
        var before = s.Ground.Snapshot();

        var (_, _, clamped) = Box(40, 40, 80, 80, soft: 2).Apply(s.Ground, s.Brush, act, 33, 5, [1, 0, 0, 0]);

        Assert.False(clamped);
        Assert.Equivalent(before, s.Ground.Snapshot(), strict: true);
    }

    [Fact]
    public void VolumeCountsEverythingWithNoLimit()
    {
        var s = TestScenes.Flat(2);
        var a = Box(40, 40, 80, 80);
        static string Fill(string text) => System.Text.RegularExpressions.Regex.Match(text, @"fill [\d.k]+ m³").Value;
        // 30 m up on 30 m ground: the limit reaches 8 m of it, No limit all of it.
        string flatten = a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 60, 0), raise = a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 30);
        Assert.Contains("out of reach: ±8 m limit", flatten);
        s.Ground.NoLimit = true;
        string flattenAll = a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 60, 0), raiseAll = a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 30);
        Assert.DoesNotContain("out of reach", flattenAll);
        Assert.NotEqual(Fill(raise), Fill(raiseAll));
        Assert.Equal(Fill(flattenAll), Fill(raiseAll));
        Assert.Equal(Fill(raiseAll).Replace("fill", "dig"), System.Text.RegularExpressions.Regex.Match(a.Volume(s.Ground, AreaTool.GroundAction.Lower, 0, 30), @"dig [\d.k]+ m³").Value);
        // Smoothing kept by the game's limit takes from the reach on its side: a point smoothed 1 m up gets down
        // to 7 m below the generated ground, not 8, as SetHeight has it.
        s.Ground.NoLimit = false;
        int q = 60 * s.Ground.W + 60;
        s.Ground.Mod[q] = 1;
        s.Ground.Smooth[q] = 1;
        var one = Box(59.5f, 59.5f, 60.5f, 60.5f);
        Assert.True(s.Ground.SetHeight(q, 20));
        Assert.Equal(23, s.Ground.HeightOf(q), 3);
        s.Ground.Level[q] = 0;
        Assert.Contains("dig 8 m³", one.Volume(s.Ground, AreaTool.GroundAction.Lower, 0, 30));
        s.Ground.Mod[q] = 0;
        s.Ground.Smooth[q] = 0;
        s.Ground.NoLimit = true;
        // The lift stops at ±400 m of the generated ground: 500 m up is 100 m out of reach. Checked on the one
        // point (weight 1): the box's outline runs through its edge points, which get half the amount, so
        // 500 m lifts them more than 400 m does.
        Assert.Contains("out of reach: ±400 m lift limit", a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 530, 0));
        Assert.Contains("fill 400 m³", one.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 400));
        Assert.Contains("fill 400 m³", one.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 500));
    }

    // Restore over No limit ground (a mountain, held as lift past the ±8 m limit) brings it back down
    // too (it changed nothing to be seen).
    [Fact]
    public void RestoreTakesAMountainAway()
    {
        var s = TestScenes.Flat(5);
        Assert.NotNull(s.Mountain(160, 160, new MountainSpec(MountainKind.Peak, 80, 60, 0.6f, 0, 42), clear: false, "Mountain: test"));
        Assert.True(H(s, 160, 160) > 80);
        var a = Box(100, 100, 220, 220);
        RestoreArea(s, a);
        Assert.Equal(30, H(s, 160, 160), 3);
        Assert.Equal(0, s.Ground.Lift[160 * s.Ground.W + 160]);
    }

    /// <summary>
    /// Restores the ground under the area, as the panel's Restore action does.
    /// </summary>
    private static void RestoreArea(EditSession s, AreaTool a) =>
        s.EditGround("Area: Restore", g =>
        {
            var (t, rect, _) = a.Apply(g, s.Brush, AreaTool.GroundAction.Restore, 0, 0, new float[4]);
            return (t, rect);
        });
}
