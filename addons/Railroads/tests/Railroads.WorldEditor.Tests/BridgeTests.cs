using System.Numerics;
using System.Reflection;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The drawbridge against what Railroads 0.3.6 did on a server running it (fixtures/railroads-bridge.txt): its lever
/// raises it as the add-on reads it, a raised bridge holds a train sent past it and the train keeps who sent it; the
/// add-on puts vehicles on a drawbridge's line, and on the track before it, as the mod did, keeps the bridge's turn and
/// stops trains at the sign beside the straight, not on the bridge. Every value the mod keeps there is one the add-on
/// names.
/// </summary>
public class BridgeTests
{
    private static IEnumerable<FakeObject> Of(string prefab) => Reference.Bridge.Select(r => r.Object).Where(o => o.Prefab == prefab);

    /// <summary>The drawbridge raised by its lever (the first along the railway).</summary>
    private static FakeObject Raised => Assert.Single(Of(RailroadsMod.Drawbridge), b => b.Ints.ContainsKey(RailroadsMod.Keys.BridgeUp));

    [Fact]
    public void ItsLeverRaisesTheDrawbridgeAsTheAddOnReadsIt()
    {
        var bridges = Of(RailroadsMod.Drawbridge).ToList();

        Assert.Equal(2, bridges.Count);
        Assert.True(DrawbridgeParts.IsUp(Raised));
        // From down, at the game's time then; the other was never swung, and keeps none of it.
        Assert.Equal(0, Raised.Floats[RailroadsMod.Keys.BridgeFrom]);
        Assert.True(Raised.Longs[RailroadsMod.Keys.BridgeSince] > 0);
        var other = Assert.Single(bridges, b => b != Raised);
        Assert.False(DrawbridgeParts.IsUp(other));
        Assert.DoesNotContain(other.Keys, k => k.StartsWith("bf_bridge_", StringComparison.Ordinal));
    }

    [Fact]
    public void ARaisedDrawbridgeHoldsATrainSentPastIt()
    {
        var train = Assert.Single(Of(RailroadsMod.Locomotive));
        var (_, end) = Assert.Single(TrackLines.Read(train.Bytes[RailroadsMod.Keys.Line])!);

        Assert.Equal((1, 0f), (train.Ints[RailroadsMod.Keys.Moving], train.Floats[RailroadsMod.Keys.Speed]));
        Assert.Equal("Bridge End", train.Strings[RailroadsMod.Keys.Destination]);
        Assert.True(train.Longs[RailroadsMod.Keys.SentBy] != 0);
        // It stands on the straight that ends where the raised bridge begins.
        Assert.True(Vector3.Distance(end, Raised.Position) < 0.01f, $"{end} is not {Raised.Position}");
    }

    [Fact]
    public void PastedOrPutSomewhereTheTrainForgetsWhoSentIt()
    {
        var train = Assert.Single(Of(RailroadsMod.Locomotive));
        var copy = ReferenceTests.Copy(train);

        new PastedCopies().Rewrite(copy, new NoIds());

        Assert.DoesNotContain(RailroadsMod.Keys.SentBy, copy.Keys);
        Assert.DoesNotContain(RailroadsMod.Keys.SentBy, ReferenceTests.Placed(train, Reference.Bridge).Keys);
    }

    public static TheoryData<string> Vehicles() => [RailroadsMod.Locomotive, "bf_wagon", "bf_wagon_restaurant"];

    [Theory]
    [MemberData(nameof(Vehicles))]
    public void AVehicleIsPutOnTheLineTheModPutItOn(string prefab)
    {
        var recorded = Assert.Single(Of(prefab));
        var placed = ReferenceTests.Placed(recorded, Reference.Bridge);

        var line = Assert.Single(TrackLines.Read(recorded.Bytes[RailroadsMod.Keys.Line])!);
        var (a, b) = Assert.Single(TrackLines.Read(placed.Bytes[RailroadsMod.Keys.Line])!);
        Assert.True(Vector3.Distance(line.A, a) < 0.01f && Vector3.Distance(line.B, b) < 0.01f, $"{a} {b} is not {line}");
        Assert.Equal(recorded.Floats[RailroadsMod.Keys.At], placed.Floats[RailroadsMod.Keys.At], 0.01f);
    }

    [Fact]
    public void TheRestaurantWagonStandsOnTheOtherDrawbridge()
    {
        var wagon = Assert.Single(Of("bf_wagon_restaurant"));
        var bridge = Assert.Single(Of(RailroadsMod.Drawbridge), b => b != Raised);

        var (a, b) = Assert.Single(TrackLines.Read(wagon.Bytes[RailroadsMod.Keys.Line])!);

        // Its line is the bridge's 16 m, the wagon halfway along it.
        Assert.True(Vector3.Distance(a, bridge.Position) < 0.01f);
        Assert.Equal(DrawbridgeParts.Length, Vector3.Distance(a, b), 0.01f);
        Assert.Equal(DrawbridgeParts.Length / 2, wagon.Floats[RailroadsMod.Keys.At], 0.01f);
    }

    [Fact]
    public void TheSignStopsTrainsBesideTheStraightNotOnTheBridge()
    {
        var recorded = Assert.Single(Of(RailroadsMod.Sign));

        var placed = ReferenceTests.Placed(recorded, Reference.Bridge);

        Assert.True(Vector3.Distance(recorded.Vectors[RailroadsMod.Keys.Stop], placed.Vectors[RailroadsMod.Keys.Stop]) < 0.01f,
            $"{placed.Vectors[RailroadsMod.Keys.Stop]} is not {recorded.Vectors[RailroadsMod.Keys.Stop]}");
    }

    [Fact]
    public void TrackAndDrawbridgesKeepTheTurnTheModKeeps()
    {
        var track = Reference.Bridge.Select(r => r.Object).Where(o => o.Quaternions.ContainsKey(RailroadsMod.Keys.Turn)).ToList();

        Assert.Equal(8, track.Count);
        foreach (var piece in track)
        {
            var turn = ReferenceTests.Placed(piece, Reference.Bridge).Quaternions[RailroadsMod.Keys.Turn];
            Assert.True(Math.Abs(Quaternion.Dot(turn, piece.Quaternions[RailroadsMod.Keys.Turn])) > 0.99999f, piece.Prefab);
        }
    }

    [Fact]
    public void TheFeedbackPostKeepsNothingOfTheMods()
    {
        var post = Assert.Single(Of("bf_feedback_post"));

        Assert.DoesNotContain(post.Keys, k => k.StartsWith("bf_", StringComparison.Ordinal));
        Assert.DoesNotContain(ReferenceTests.Placed(post, Reference.Bridge).Keys, k => k.StartsWith("bf_", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryValueTheModKeepsThereIsOneTheAddOnNames()
    {
        var named = typeof(RailroadsMod.Keys).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!)
            .Concat(["creator", "creatorIndex", "InUse", "addedDefaultItems", "StartTime"]).ToHashSet();

        Assert.All(Reference.Bridge.SelectMany(r => r.Object.Keys), k => Assert.Contains(k, named));
    }
}
