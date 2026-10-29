using System.Numerics;
using System.Reflection;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Regrow's look rotation: the rotation turns the forward direction onto the one given, keeping the up direction. Regrow
/// uses the world generator (static state): one world at a time.
/// </summary>
[Collection("World files")]
public class RegrowLookRotationTests
{
    private static readonly MethodInfo LookRotation = typeof(Regrow).GetMethod("LookRotation", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 1f, 0f)]
    [InlineData(0f, 0f, -1f, 0f, 1f, 0f)]
    [InlineData(0f, 0f, -1f, 0f, -1f, 0f)]
    [InlineData(0f, 0f, 1f, 0f, -1f, 0f)]
    [InlineData(0.3f, 0.2f, 0.9f, 0.1f, 1f, 0.2f)]
    public void LookRotationTurnsForwardOntoTheGivenDirection(float fx, float fy, float fz, float ux, float uy, float uz)
    {
        var forward = Vector3.Normalize(new Vector3(fx, fy, fz));
        var up = new Vector3(ux, uy, uz);
        var q = (Quaternion)LookRotation.Invoke(null, [forward, up])!;
        Assert.Equal(1f, q.Length(), 3);
        var f = Vector3.Transform(Vector3.UnitZ, q);
        Assert.True(Vector3.Distance(f, forward) < 1e-3f, $"{f} for {forward}");
        // Up stays on the side of the given up (made square to forward).
        Assert.True(Vector3.Dot(Vector3.Transform(Vector3.UnitY, q), up) > 0, $"{q}");
    }
}
