using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How a part is placed on what it hangs on beyond its rule's own keys: a manifest's placements (a parent key's own
/// offset and turn keys; a part carried where it is saved), and an extension's link placer (the frame a parent's parts
/// hang in, a part placed by the extension). Runs alone (the manifests' rules and the extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class ObjectLinksPlacementsTests : ModManifestsTestBase
{
    // A made-up mod's rafts: a sail hangs on its mast by its own keys, a crate is carried where it was saved.
    private const string RaftsWith = """
        {
          "version": 1,
          "links": [
            {
              "name": "raft",
              "id": "RaftId",
              "parents": ["RaftMast", "RaftParent", "RaftCarrier"],
              "offset": "RaftOffset",
              "rotation": "RaftTurn",
              "placements": PLACEMENTS
            }
          ]
        }
        """;

    /// <summary>The rafts' placements.</summary>
    private const string Placements = """{ "RaftMast": { "offset": "MastOffset", "rotation": "MastTurn" }, "RaftCarrier": { "carried": true } }""";

    /// <summary>The rafts' manifest.</summary>
    private static readonly string Rafts = RaftsWith.Replace("PLACEMENTS", Placements, StringComparison.Ordinal);

    private static int H(string key) => StableHash.Of(key);

    /// <summary>The rafts' rule, as in use.</summary>
    private static LinkRule Rule => ObjectLinks.Current.Rules.Single(r => r.IdKey == H("RaftId"));

    private void Install(string? json = null)
    {
        Manifest("Rafts", json ?? Rafts);
        ModManifests.Refresh(Settings);
    }

    /// <summary>Whether two places are within a millimetre.</summary>
    private static void Near(Vector3 want, Vector3 got) => Assert.True(Vector3.Distance(want, got) < 1e-3f, $"{want} != {got}");

    [Fact]
    public void APlacementGivesThePartsOnItsParentKeyTheirOwnKeys()
    {
        Install();
        var rule = Rule;

        var mast = rule.PlacementAt(0);
        Assert.Equal((H("MastOffset"), H("MastTurn"), 0, true, false), (mast.OffsetKey, mast.EulerKey, mast.QuatKey, mast.StoredAtRoot, mast.Carried));
        var own = rule.PlacementAt(1);
        Assert.Equal((H("RaftOffset"), H("RaftTurn"), true, false), (own.OffsetKey, own.EulerKey, own.StoredAtRoot, own.Carried));
        var carried = rule.PlacementAt(2);
        Assert.Equal((0, 0, false, true), (carried.OffsetKey, carried.EulerKey, carried.StoredAtRoot, carried.Carried));
        Assert.True(ObjectLinks.IsLinkKey(H("MastOffset")));
        Assert.True(ObjectLinks.IsLinkKey(H("MastTurn")));

        // A sail on its mast (and on the raft): its place and turn are read under the mast's keys.
        var z = new ZdoData();
        z.SetInt(H("RaftMast"), 7);
        z.SetInt(H("RaftParent"), 5);
        z.SetVec3(H("MastOffset"), new Vector3(1, 2, 3));
        z.SetVec3(H("MastTurn"), new Vector3(0, 90, 0));
        z.SetVec3(H("RaftOffset"), new Vector3(9, 9, 9));
        var link = ObjectLinks.Read(z)!;
        Assert.Equal((7, 0, new Vector3(1, 2, 3)), (link.Holder, link.HolderIndex, link.Offset));
        Assert.Equal(new Vector3(0, 90, 0), link.Euler);
        Assert.False(link.Carried);
        Assert.True(link.StoredAtRoot);
    }

    [Theory]
    [InlineData("""{ "Nope": { "carried": true } }""")]
    [InlineData("""{ "RaftCarrier": { "carried": true, "offset": "CrateOffset" } }""")]
    [InlineData("""{ "RaftMast": { "storedAt": "sideways" } }""")]
    [InlineData("""{ "RaftMast": "MastOffset" }""")]
    [InlineData("""[]""")]
    public void ARuleWhosePlacementsAreNotInTheirFormIsLeftOut(string placements)
    {
        Install(RaftsWith.Replace("PLACEMENTS", placements, StringComparison.Ordinal));

        Assert.DoesNotContain(ObjectLinks.Current.Rules, r => r.IdKey == H("RaftId"));
    }

    // A raft turned 90 degrees at (10, 30, 10), a crate it carries saved at (12, 31, 9) turned 45 degrees: drawn there.
    // On a deck hanging 5 m east of a raft at the world's middle, a crate saved 1 m east of the deck and 1 m up stays
    // so, in the deck's frame, wherever the deck is drawn.
    [Fact]
    public void ACarriedPartIsDrawnWhereItIsSavedRelativeToWhatCarriesIt()
    {
        Install();
        var rule = Rule;
        var raft = new ObjectLink(rule, 5, [0, 0, 0], Vector3.Zero, null, null);
        var crate = new ObjectLink(rule, 0, [0, 0, 5], new Vector3(50, 50, 50), null, null);
        Assert.True(crate.Carried);
        Assert.False(crate.StoredAtRoot);

        var ix = LinkIndex.Of([new(1, new Vector3(10, 30, 10), new Vector3(0, 90, 0), raft), new(2, new Vector3(12, 31, 9), new Vector3(0, 45, 0), crate)]);

        var (at, turn) = ix.Pose(2)!.Value;
        Near(new Vector3(12, 31, 9), at);
        Assert.True(Math.Abs(Quaternion.Dot(BlueprintFormats.FromEuler(new Vector3(0, 45, 0)), BlueprintFormats.FromEuler(turn))) > 1 - 1e-5f);
        Assert.Equal([1, 2], ix.Group(2));

        var deck = new ObjectLink(rule, 6, [0, 5, 0], new Vector3(5, 0, 0), Vector3.Zero, null);
        var load = new ObjectLink(rule, 0, [0, 0, 6], Vector3.Zero, null, null);
        var at0 = new Vector3(0, 30, 0);
        var ix2 = LinkIndex.Of(
        [
            new(1, at0, new Vector3(0, 90, 0), raft),
            new(2, at0, Vector3.Zero, deck),
            new(3, new Vector3(1, 31, 0), Vector3.Zero, load),
        ]);
        var deckAt = ix2.Pose(2)!.Value.Position;
        Near(at0 + Vector3.Transform(new Vector3(5, 0, 0), BlueprintFormats.FromEuler(new Vector3(0, 90, 0))), deckAt);
        Near(deckAt + Vector3.Transform(new Vector3(1, 1, 0), BlueprintFormats.FromEuler(new Vector3(0, 90, 0))), ix2.Pose(3)!.Value.Position);
    }

    // A pasted crate stays where it was pasted (not at its raft), carried by the copy of its raft; a part stored at its
    // root goes there.
    [Fact]
    public void APastedCarriedPartStaysWhereItIsPastedCarriedByTheCopy()
    {
        Install();
        static ObjectField Int(string key, int value) => new("ints", H(key), value.ToString(CultureInfo.InvariantCulture));
        int prefab = StableHash.Of("raft_piece");

        var copy = ObjectLinks.Remap(
        [
            new(0, prefab, new Vector3(10, 30, 10), Vector3.Zero, 0, Data: [Int("RaftId", 5)]),
            new(0, prefab, new Vector3(12, 31, 9), Vector3.Zero, 0, Data: [Int("RaftCarrier", 5)]),
            new(0, prefab, new Vector3(11, 30, 10), Vector3.Zero, 0, Data: [Int("RaftParent", 5), new ObjectField("vec3", H("RaftOffset"), "1 0 0")]),
        ], new HashSet<int> { 5 });

        string id = copy[0].Data!.Single(f => f.Key == H("RaftId")).Value!;
        Assert.NotEqual("5", id);
        Assert.Equal(id, copy[1].Data!.Single(f => f.Key == H("RaftCarrier")).Value);
        Assert.Equal(new Vector3(12, 31, 9), copy[1].Position);
        Assert.Equal(new Vector3(10, 30, 10), copy[2].Position);
    }

    /// <summary>An extension placing for turntables (their parts hang on them 1 m up, turned as their data says) and riders (5 m up their parent).</summary>
    private sealed class Turntables : IWorldEditorExtension, ILinkPlacer
    {
        public void Register(IExtensionRegistry registry) => registry.AddLinkPlacer(this, "test_turntable", "test_rider");

        public LinkFrame? FrameOf(IObjectView obj) =>
            obj.TryGetInt("Turned", out int degrees) ? new LinkFrame(Vector3.UnitY, Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180)) : null;

        public LinkFrame? PlaceOf(IObjectView part, IObjectView parent) =>
            part.Prefab == "test_rider" ? new LinkFrame(new Vector3(0, 5, 0), Quaternion.Identity) : null;
    }

    // A hull at (0, 30, 0); a turntable 2 m east of it, turned 90 degrees; a piece 1 m east on the turntable; a rider
    // whose data says 9 m every way, which its extension places 5 m up.
    [Fact]
    public void AnExtensionsPlacerTurnsAParentsPartsAndPlacesAPart()
    {
        ModExtensions.UseForTests(("Turntables", new Turntables()));
        try
        {
            var at = new Vector3(0, 30, 0);
            static ObjectLink Link(int id, int mount, int vehicle, Vector3 offset) => new(ExampleMod.Rule, id, [mount, vehicle], offset, Vector3.Zero, null);
            LinkIndex.Member[] members =
            [
                new(1, at, Vector3.Zero, Link(10, 0, 0, Vector3.Zero)) { Prefab = StableHash.Of(ExampleMod.Hull) },
                new(2, at, Vector3.Zero, Link(11, 0, 10, new Vector3(2, 0, 0))) { Prefab = StableHash.Of("test_turntable") },
                new(3, at, Vector3.Zero, Link(0, 11, 10, new Vector3(1, 0, 0))) { Prefab = StableHash.Of("wood_floor") },
                new(4, at, Vector3.Zero, Link(0, 0, 10, new Vector3(9, 9, 9))) { Prefab = StableHash.Of("test_rider") },
            ];
            var turned = new ZdoData();
            turned.SetInt(StableHash.Of("Turned"), 90);

            var ix = LinkIndex.Of(members, id => id == 2 ? turned : new ZdoData());

            var quarter = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
            Near(new Vector3(2, 31, 0) + Vector3.Transform(Vector3.UnitX, quarter), ix.Pose(3)!.Value.Position);
            Near(new Vector3(0, 35, 0), ix.Pose(4)!.Value.Position);
            // Without the objects' data, the placer is not asked: the links alone.
            var plain = LinkIndex.Of(members);
            Near(new Vector3(3, 30, 0), plain.Pose(3)!.Value.Position);
            Near(new Vector3(9, 39, 9), plain.Pose(4)!.Value.Position);
        }
        finally
        {
            ModExtensions.Forget();
        }
    }
}
