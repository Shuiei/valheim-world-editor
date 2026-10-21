using System.Numerics;
using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// A blueprint's terrain snapshot as Expand World Data reads it: the node nearest to a point with the blueprint placed
/// and turned, how far its nodes reach, and the turn about y it keeps.
/// </summary>
public class TerrainSnapshotTests
{
    /// <summary>3 × 3 nodes 2 m apart round the blueprint's centre, valued by their place; the middle one has none.</summary>
    private static readonly TerrainSnapshot Grid = new(Vector3.Zero, Quaternion.Identity, 2f,
        [.. Enumerable.Range(0, 3).Select(z => Enumerable.Range(0, 3).Select(x => x == 1 && z == 1 ? (Vector4?)null : new Vector4(10 * z + x, 0, 0, 0)).ToArray())]);

    private static readonly Vector3 At = new(100, 50, 100);

    [Fact]
    public void APointTakesTheNearestNodesValue()
    {
        Assert.Equal(12f, Grid.Nearest(At + new Vector3(2, 0, 0), At, Quaternion.Identity)?.X);
        Assert.Equal(21f, Grid.Nearest(At + new Vector3(0.9f, 0, 2.2f), At, Quaternion.Identity)?.X);
        // The middle node has no value; past the grid there is none.
        Assert.Null(Grid.Nearest(At, At, Quaternion.Identity));
        Assert.Null(Grid.Nearest(At + new Vector3(5.2f, 0, 0), At, Quaternion.Identity));
    }

    [Fact]
    public void TheGridTurnsWithTheBlueprintAboutYOnly()
    {
        var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        // The node 2 m along the blueprint's x lies where its turn takes that.
        Assert.Equal(12f, Grid.Nearest(At + Vector3.Transform(new Vector3(2, 0, 0), quarter), At, quarter)?.X);
        // A tilt is not followed.
        var tilted = quarter * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f);
        Assert.Equal(12f, Grid.Nearest(At + Vector3.Transform(new Vector3(2, 0, 0), quarter), At, tilted)?.X);
    }

    [Fact]
    public void ItsReachIsItsFarthestNodeWithAValue()
    {
        Assert.Equal(MathF.Sqrt(8f), Grid.Reach(), 4);
        var one = new TerrainSnapshot(new Vector3(3, 0, 4), Quaternion.Identity, 1f, [[new Vector4(1, 0, 0, 0)]]);
        Assert.Equal(5f, one.Reach(), 4);
    }

    [Fact]
    public void YawKeepsTheTurnAboutY()
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f);
        var yaw = TerrainSnapshot.Yaw(turn * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f));
        Assert.True(MathF.Abs(Quaternion.Dot(turn, yaw)) > 0.9999f, yaw.ToString());
        Assert.Equal(Quaternion.Identity, TerrainSnapshot.Yaw(Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2)));
    }
}
