using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Objects tied to a root that names them (ObjectTies, a manifest's "ties"): read from the manifests, a root's group made
/// of the members naming one of its ids (a list of ids in a byte array, or a single int or long), and a pasted copy given
/// ids of its own: the root and its copied members renamed alike, members whose root was not copied let go, a list kept
/// to the members copied. Runs alone, without the player's own Valheim and profiles.
/// </summary>
[Collection("DataDir")]
public sealed class ObjectTiesTests : ModManifestsTestBase
{
    // A made-up mod's trains (a list of 12-byte ids in a byte array; each wagon names itself) and decks (a long).
    private const string Trains = """
        {
          "version": 1,
          "ties": [
            { "name": "train", "root": "Wagons", "member": "Self", "idBytes": 12 },
            { "root": "DeckKey", "member": "OnDeck" },
            { "root": "Broken" },
            { "root": "R", "member": "M", "idBytes": 0 }
          ]
        }
        """;

    private static readonly TieRule Train = new("train", StableHash.Of("Wagons"), StableHash.Of("Self"), 12);
    private static readonly TieRule Deck = new(LinkRule.DefaultName, StableHash.Of("DeckKey"), StableHash.Of("OnDeck"), 0);

    private static byte[] Id(byte n) => [n, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0];

    private static ObjectField Bytes(string key, params byte[][] ids) => new("bytes", StableHash.Of(key), Convert.ToBase64String([.. ids.SelectMany(i => i)]));

    private static ObjectField Long(string key, long v) => new("longs", StableHash.Of(key), v.ToString(CultureInfo.InvariantCulture));

    private static NewObject Made(params ObjectField[] data) => new(0, StableHash.Of("thing"), Vector3.Zero, Vector3.Zero, 0, Data: data);

    private static ZdoData Data(NewObject o)
    {
        var z = new ZdoData();
        foreach (var f in o.Data!)
        {
            z.Set(f.Section, f.Key, f.Value);
        }
        return z;
    }

    private static string? Value(NewObject o, string key) => o.Data!.Single(f => f.Key == StableHash.Of(key)).Value;

    [Fact]
    public void AManifestsTiesAreRead()
    {
        Manifest("Trains", Trains);

        Assert.Equal(1, ModManifests.Refresh(Settings));

        Assert.Equal([Train, Deck], ObjectTies.Current);
        ModManifests.Forget();
        Assert.Empty(ObjectTies.Current);
    }

    [Fact]
    public void ARootsGroupIsTheMembersNamingItsIds()
    {
        NewObject[] objects =
        [
            Made(Bytes("Wagons", Id(1), Id(2), Id(9))),
            Made(Bytes("Self", Id(2))),
            Made(Bytes("Self", Id(1))),
            Made(Bytes("Self", Id(5))),
            Made(Long("DeckKey", 77)),
            Made(Long("OnDeck", 77)),
            Made(Long("OnDeck", 78)),
        ];

        var groups = ObjectTies.Groups([.. objects.Select((o, i) => (i, Data(o)))], [Train, Deck]);

        // The train's wagons in its order (the one it names that is not there left out), the deck's piece; a wagon
        // and a piece no root names stand alone.
        Assert.Equal(2, groups.Count);
        Assert.Equal(([0, 2, 1], Train), (groups[0].Group, groups[0].Rule));
        Assert.Equal(([4, 5], Deck), (groups[1].Group, groups[1].Rule));
    }

    [Fact]
    public void APastedTrainKeepsItsCopiedWagonsUnderIdsOfItsOwn()
    {
        NewObject[] copy =
        [
            Made(Bytes("Wagons", Id(1), Id(2), Id(3))),
            Made(Bytes("Self", Id(2)), new ObjectField("ints", StableHash.Of("Coal"), "5")),
            Made(Bytes("Self", Id(1))),
            Made(Bytes("Self", Id(8))),
        ];

        var made = ObjectTies.Remap(copy, [Train], new Random(3));

        byte[] wagons = Convert.FromBase64String(Value(made[0], "Wagons")!);
        // The copy's two wagons, in the train's order; the third was not copied.
        Assert.Equal(24, wagons.Length);
        Assert.Equal(Value(made[2], "Self"), Convert.ToBase64String(wagons[..12]));
        Assert.Equal(Value(made[1], "Self"), Convert.ToBase64String(wagons[12..]));
        Assert.NotEqual(Convert.ToBase64String(Id(1)), Value(made[2], "Self"));
        Assert.Equal("5", Value(made[1], "Coal"));
        // A wagon of another train, not copied with it: no longer one.
        Assert.Null(Value(made[3], "Self"));
    }

    [Fact]
    public void EachPastedCopyGetsIdsOfItsOwn()
    {
        NewObject[] copy = [Made(Long("DeckKey", 77)), Made(Long("OnDeck", 77))];

        var first = ObjectTies.Remap(copy, [Deck], new Random(1));
        var second = ObjectTies.Remap(copy, [Deck], new Random(2));

        Assert.Equal(Value(first[0], "DeckKey"), Value(first[1], "OnDeck"));
        Assert.NotEqual("77", Value(first[0], "DeckKey"));
        Assert.NotEqual(Value(first[0], "DeckKey"), Value(second[0], "DeckKey"));
    }

    [Fact]
    public void ARootAloneStillGetsANewSingleId()
    {
        var made = ObjectTies.Remap([Made(Long("DeckKey", 77)), Made(Long("OnDeck", 70))], [Deck], new Random(4));

        Assert.NotEqual("77", Value(made[0], "DeckKey"));
        Assert.Null(Value(made[1], "OnDeck"));
    }

    [Fact]
    public void ObjectsWithoutTiesComeBackAsTheyWere()
    {
        NewObject[] copy = [Made(new ObjectField("ints", StableHash.Of("Coal"), "5")), new(0, 1, Vector3.Zero, Vector3.Zero, 0)];

        var made = ObjectTies.Remap(copy, [Train, Deck], new Random(5));

        Assert.Same(copy[0], made[0]);
        Assert.Same(copy[1], made[1]);
        Assert.False(ObjectTies.AnyIn(copy, [Train, Deck]));
    }
}
