using System.Numerics;
using System.Reflection;
using WorldEditor.Tests;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// What the real Railroads keeps when a player builds track and stations (fixtures/railroads-supports.txt, recorded on a
/// server running it, the mod's own placement path run by a stand-in player): every key it wrote is one the add-on
/// knows (so the inspector names it); the support poles are the game's own pieces holding what they are made of and
/// which part they are, posts and knees; prices on the track; and the old station built on supports, without its legs,
/// drawn so.
/// </summary>
public class SupportsTests
{
    /// <summary>Every data key the code names (RailroadsMod.Keys).</summary>
    private static readonly HashSet<string> Known =
        [.. typeof(RailroadsMod.Keys).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!)];

    private static IEnumerable<FakeObject> All => Reference.Objects.Concat(Reference.Supports).Select(r => r.Object);

    [Fact]
    public void EveryKeyTheModWroteIsOneTheAddOnKnows()
    {
        var written = All.SelectMany(o => o.Keys).Where(k => k.StartsWith("bf_", StringComparison.Ordinal)).ToHashSet();

        Assert.Empty(written.Except(Known));
        Assert.Contains(RailroadsMod.Keys.Leg, written);
        Assert.Contains(RailroadsMod.Keys.PaidCoreWood, written);
    }

    [Fact]
    public void ThePolesAreTheGamesPiecesSayingWhatTheyAre()
    {
        var poles = Reference.Supports.Select(r => r.Object).Where(o => o.Ints.ContainsKey(RailroadsMod.Keys.Leg)).ToList();

        Assert.Equal(64, poles.Count);
        Assert.All(poles, p => Assert.False(p.Prefab.StartsWith("bf_", StringComparison.Ordinal)));
        Assert.All(poles, p => Assert.InRange(p.Ints[RailroadsMod.Keys.Leg], 1, 3));
        // A post (part 0) is a pole, a knee (part 5) a 45° beam.
        Assert.All(poles, p => Assert.Equal(p.Prefab.EndsWith("_45", StringComparison.Ordinal) ? 5 : 0, p.Ints[RailroadsMod.Keys.LegPart]));
        Assert.Contains(poles, p => p.Ints.GetValueOrDefault(RailroadsMod.Keys.LegFree) == 1);
    }

    [Fact]
    public void TrackBuiltByAPlayerKeepsWhatItCost()
    {
        var paid = Reference.Supports.Select(r => r.Object).Where(o => o.Ints.ContainsKey(RailroadsMod.Keys.Paid)).ToList();

        Assert.NotEmpty(paid);
        Assert.All(paid, t => Assert.Equal((1, 8, 16), (t.Ints[RailroadsMod.Keys.Paid], t.Ints[RailroadsMod.Keys.PaidWood], t.Ints[RailroadsMod.Keys.PaidStone])));
        Assert.Single(paid, t => t.Ints.GetValueOrDefault(RailroadsMod.Keys.PaidCoreWood) == 8);
    }

    [Fact]
    public void TheOldStationOnSupportsIsDrawnWithoutItsLegs()
    {
        var station = Assert.Single(Reference.Supports, r => r.Object.Prefab == RailroadsMod.OldStation).Object;
        var models = new ModelLibrary(new FakeHost { Plugin = Path.Combine(TestMods.Folder, "VladasZ-Railroads", "plugins", "Railroads", RailroadsMod.DllName) }, Assert.Fail);
        var piece = RailroadsMod.Pieces.Single(p => p.Prefab == RailroadsMod.OldStation);

        var shapes = new PieceShapes(piece, models).ShapesOf(station, new NoWorld()).ToList();

        Assert.Equal(1, station.Ints[RailroadsMod.Keys.Bare]);
        Assert.Equal(models.Get(piece.Model)!.Parts.Count, shapes.Count);
        Assert.True(Math.Abs(Quaternion.Dot(station.Quaternions[RailroadsMod.Keys.Turn], station.Orientation)) > 0.99999f);
    }
}
