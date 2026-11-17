using System.Numerics;
using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The ground actions written once (GroundOps): what each does to a point, and that a brush, the Area tool and the
/// Path tool, which only say where, give the same ground for the same action.
/// </summary>
public class GroundOpsTests
{
    /// <summary>A grid point well inside the 2 × 2 zone test ground: (64, 64).</summary>
    private const int Middle = 64 * 129 + 64;

    /// <summary>A flat 129 × 129 ground at 30 m with a 4 m bump on the middle point.</summary>
    private static Ground Bumped()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        g.SetHeight(Middle, 34f);
        return g;
    }

    /// <summary>A box selection with no soft edge over grid points (50, 50) to (78, 78).</summary>
    private static AreaTool Box()
    {
        var a = new AreaTool { Soft = 0 };
        a.Points.Add(new Vector2(50, 50));
        a.Points.Add(new Vector2(78, 78));
        return a;
    }

    /// <summary>A straight line through the middle, 12 m wide with no soft edge.</summary>
    private static PathTool Line(PathTool.Action act)
    {
        var p = new PathTool { Act = act, Width = 12, Soft = 0, Curved = false };
        p.Points.Add(new Vector2(40, 64));
        p.Points.Add(new Vector2(88, 64));
        return p;
    }

    [Fact]
    public void MoveRaisesAndLowersByMetres()
    {
        var g = Bumped();
        Assert.False(GroundOps.Move(g, Middle, 1.5f));
        Assert.Equal(35.5f, g.HeightOf(Middle), 3);
        GroundOps.Move(g, Middle, -3f);
        Assert.Equal(32.5f, g.HeightOf(Middle), 3);
        // Past the game's ±8 m from the generated ground: stopped, and said.
        Assert.True(GroundOps.Move(g, Middle, 20f));
        Assert.Equal(38f, g.HeightOf(Middle), 3);
    }

    [Fact]
    public void TowardGoesAShareOfTheWayAndNeverPast()
    {
        var g = Bumped();
        GroundOps.Toward(g, Middle, 30f, 0.25f);
        Assert.Equal(33f, g.HeightOf(Middle), 3);
        GroundOps.Toward(g, Middle, 30f, 1f);
        Assert.Equal(30f, g.HeightOf(Middle), 3);
    }

    [Fact]
    public void RestoreTakesTheEditBackByItsShareAndForGoodNearTheEnd()
    {
        var g = Bumped();
        GroundOps.Restore(g, Middle, 0.5f, 0.5f);
        Assert.Equal(32f, g.HeightOf(Middle), 3);
        Assert.Equal((byte)1, g.Mod[Middle]);
        // Above 0.98 of the way: the point is the generated ground again, its edit gone.
        GroundOps.Restore(g, Middle, 0.99f, 0.99f);
        Assert.Equal(30f, g.HeightOf(Middle));
        Assert.Equal((byte)0, g.Mod[Middle]);
    }

    [Fact]
    public void PaintMovesThePaintAShareOfTheWayToTheColour()
    {
        var g = Bumped();
        GroundOps.Paint(g, Middle, [1f, 0f, 0f, 1f], 0.5f);
        float half = g.Paint[Middle * 4];
        GroundOps.Paint(g, Middle, [1f, 0f, 0f, 1f], 1f);
        Assert.Equal((byte)1, g.PMod[Middle]);
        Assert.Equal(1f, g.Paint[Middle * 4], 4);
        Assert.InRange(half, 0.49f, 1f);
    }

    [Fact]
    public void SmoothGivesTheSameGroundInASelectionAndAlongALine()
    {
        Ground area = Bumped(), line = Bumped();
        float expected = HeightsBefore.Take(area, 64, 64, 64, 64, GroundOps.SmoothReach).Average(Middle, GroundOps.SmoothReach);

        Box().Apply(area, new Brush(), AreaTool.GroundAction.Smooth, 0, 0, new float[4]);
        Line(PathTool.Action.Smooth).Apply(line, new Brush(), 0);

        // The 7 × 7 square round the bump: 4 m shared between 49 points.
        Assert.Equal(30f + 4f / 49, expected, 3);
        Assert.Equal(expected, area.HeightOf(Middle), 3);
        Assert.Equal(expected, line.HeightOf(Middle), 3);
        // A neighbour of the bump too, in both.
        Assert.Equal(area.HeightOf(Middle + 1), line.HeightOf(Middle + 1), 3);
    }

    [Fact]
    public void NaturalizeAimsAtTheSameGroundUnderABrushInASelectionAndAlongALine()
    {
        var b = new Brush { NoiseAmp = 1.5f, NoiseSize = 12, Noise = new Noise(11) };
        Ground area = Bumped(), line = Bumped(), brushed = Bumped();

        Box().Apply(area, b, AreaTool.GroundAction.Natural, 0, 0, new float[4]);
        Line(PathTool.Action.Naturalize).Apply(line, b, 0);
        var stroke = new Stroke { Tool = BrushTool.Natural, Start = brushed.Snapshot() };
        float aim = (float)typeof(Sculpt).GetMethod("NaturalTarget", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [brushed, b, stroke, Middle])!;

        Assert.Equal(area.HeightOf(Middle), line.HeightOf(Middle), 3);
        Assert.Equal(area.HeightOf(Middle), aim, 3);
        // The bumps are the brush's: another pattern, another ground.
        var other = Bumped();
        Box().Apply(other, new Brush { NoiseAmp = 1.5f, NoiseSize = 12, Noise = new Noise(12) }, AreaTool.GroundAction.Natural, 0, 0, new float[4]);
        Assert.NotEqual(area.HeightOf(Middle), other.HeightOf(Middle), 3);
    }

    [Fact]
    public void RestoreGivesTheGeneratedGroundBackInASelectionAndAlongALine()
    {
        Ground area = Bumped(), line = Bumped();
        GroundOps.Paint(area, Middle, [1f, 0f, 0f, 1f], 1f);
        GroundOps.Paint(line, Middle, [1f, 0f, 0f, 1f], 1f);

        Box().Apply(area, new Brush(), AreaTool.GroundAction.Restore, 0, 0, new float[4]);
        var (touched, _, clamped) = Line(PathTool.Action.Restore).Apply(line, new Brush(), 0);

        Assert.Contains(Middle, touched);
        Assert.False(clamped);
        foreach (var g in new[] { area, line })
        {
            Assert.Equal(30f, g.HeightOf(Middle));
            Assert.Equal((byte)0, g.Mod[Middle]);
            Assert.Equal((byte)0, g.PMod[Middle]);
        }
    }

    [Fact]
    public void ErodeAlongALineWeathersOnlyTheGroundTheLineReaches()
    {
        var g = new Ground(129, 129, 0, 0, 2);
        // A cliff across the line: 30 m west of x = 64, 36 m east of it.
        for (int i = 0; i < g.Base.Length; i++)
        {
            g.Base[i] = i % g.W < 64 ? 30f : 36f;
        }
        var b = new Brush { RestAngle = 30, Random = new Random(3) };
        var (touched, rect, _) = Line(PathTool.Action.Erode).Apply(g, b, 0);

        Assert.NotEmpty(touched);
        // Every point changed is within the line's half width (6 m) and the half metre its hard edge shares.
        Assert.All(touched, p => Assert.InRange(p / g.W, 57, 71));
        // The foot of the cliff was filled and its top worn down, on the line.
        Assert.True(g.HeightOf(64 * g.W + 63) > 30f);
        Assert.True(g.HeightOf(64 * g.W + 64) < 36f);
        // Away from the line the cliff stands.
        Assert.Equal(36f, g.HeightOf(100 * g.W + 64));
        Assert.True(rect.X0 <= 63 && rect.X1 >= 64);
    }
}
