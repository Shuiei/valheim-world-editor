using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A building piece placed by Alt's quarter metre on sloping ground stands on the ground where it goes, not at the
/// height of the point the cursor was on.
/// </summary>
public class PlaceToolBuildTests
{
    [AvaloniaFact]
    public void AFinePieceStandsOnTheGroundWhereItGoes()
    {
        var session = TestScenes.Flat(2);
        var s = session.Scene;
        // Ground rising half a metre a metre eastwards.
        for (int gz = 0; gz < s.H; gz++)
        {
            for (int gx = 0; gx < s.W; gx++)
            {
                s.Heights[gz * s.W + gx] = 30 + 0.5f * gx;
            }
        }
        var t = new PlaceTool { Scene = () => s, Building = true, Fine = true, NameOf = h => h == StableHash.Of("woodwall") ? "woodwall" : null };
        t.Chosen.Add("woodwall");
        float ox = s.OriginX, oz = s.OriginZ;
        // Straight down onto grid point 40.37, 50.1 (the ground there at 30 + 0.5 × 40.37).
        var at = new Vector3(ox + 40.37f, 0, oz + 50.1f);
        const float ground = 30 + 0.5f * 40.37f;
        t.AimRay = (at with { Y = 100 }, -Vector3.UnitY, 100 - ground);

        var fine = t.Preview(new Vector2(40.37f, 50.1f)).Single();
        t.Fine = false;
        var free = t.Preview(new Vector2(40.37f, 50.1f)).Single();

        static float GroundAt(float gx) => 30 + 0.5f * gx;
        float mx = ox + (s.W - 1) / 2f;
        float quarters = (fine.Position.X - mx) * 4;
        Assert.Equal(MathF.Round(quarters), quarters, 3);
        Assert.NotEqual(free.Position.X, fine.Position.X, 3);
        // Moved along the slope, it stands on the ground where it now is, as high above it as the piece not rounded.
        Assert.Equal(free.Position.Y - GroundAt(free.Position.X - ox), fine.Position.Y - GroundAt(fine.Position.X - ox), 3);
        Assert.NotEqual(free.Position.Y, fine.Position.Y, 3);
    }
}
