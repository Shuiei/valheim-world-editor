using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The flat ground the Hammer tests place pieces on, the hit of a ray looking straight down at it, and the millimetre check
/// of the points that come back.
/// </summary>
internal static class HammerTestScene
{
    /// <summary>
    /// The height of the ground the test pieces stand on.
    /// </summary>
    internal const float Ground = 30;

    /// <summary>
    /// No turn: the identity rotation.
    /// </summary>
    internal static readonly Quaternion None = Quaternion.Identity;

    /// <summary>
    /// Asserts two points are within a millimetre.
    /// </summary>
    internal static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    /// <summary>
    /// The hammer's hit looking straight down at x, z from 50 m above the ground.
    /// </summary>
    internal static Hammer.Hit Down(float x, float z, IEnumerable<Hammer.Placed> pieces)
    {
        var o = new Vector3(x, Ground + 50, z);
        return Hammer.Ray(o, -Vector3.UnitY, pieces, 50)!;
    }
}
