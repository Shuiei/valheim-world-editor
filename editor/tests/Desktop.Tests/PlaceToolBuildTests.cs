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
        float ground = 30 + 0.5f * 40.37f;
        t.AimRay = (at with { Y = 100 }, -Vector3.UnitY, 100 - ground);

        var piece = t.Preview(new Vector2(40.37f, 50.1f)).Single();

        float mx = ox + (s.W - 1) / 2f;
        float x = piece.Position.X - ox;
        float quarters = (piece.Position.X - mx) * 4;
        Assert.Equal(MathF.Round(quarters), quarters, 3);
        // A wall's foot is a metre under its middle: it stands on the ground at its rounded place.
        Assert.Equal(30 + 0.5f * x + 1, piece.Position.Y, 2);
        Assert.NotEqual(ground + 1, piece.Position.Y, 2);
    }
}
