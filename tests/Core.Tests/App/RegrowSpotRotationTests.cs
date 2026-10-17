using TerrainEditor.App;
using Xunit;
using NVector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// The rotation a regrown spot is planted with: a rule that never uses the ground's tilt keeps the rotation drawn for
/// the spot as it is.
/// </summary>
public sealed class RegrowSpotRotationTests
{
    /// <summary>With no ground tilt in the rule, the spot keeps the rotation drawn for it.</summary>
    [Fact]
    public void WithNoGroundTiltTheSpotKeepsItsDrawnRotation()
    {
        object rule = RegrowFixtures.OpenRule();
        PrivateMember.Set(rule, "chanceToUseGroundTilt", 0f);
        object draw = PrivateMember.New(PrivateMember.Nested(typeof(Regrow), "SpotDraw"), 10f, 20f, 30f, 1f);

        var rotation = PrivateMember.Call<NVector3>(typeof(Regrow), "SpotRotation", null, rule, NVector3.UnitY, draw);

        Assert.Equal(new NVector3(10f, 20f, 30f), rotation);
    }
}
