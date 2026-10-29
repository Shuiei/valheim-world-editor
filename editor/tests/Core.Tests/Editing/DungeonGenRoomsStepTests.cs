using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The steps that make a dungeon of the game's rooms (DungeonGen.Rooms and its parts): the kind of dungeon, the joined
/// rooms, the end caps and the items.
/// </summary>
public class DungeonGenRoomsStepTests
{
    /// <summary>The ordinary rooms and the entrance and end-cap rooms of the kind of dungeon, as the generator lists them.</summary>
    private static (List<Dungeons.Room> Entrances, List<Dungeons.Room> Normal, List<Dungeons.Room> Caps, Dungeons.Kind Kind) RoomsOf(string kindName)
    {
        var kind = Dungeons.KindOf(StableHash.Of(kindName))!;
        var all = Dungeons.RoomsFor(kind).ToList();
        return (all.Where(r => r.Entrance && !r.EndCap).ToList(),
            all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList(),
            all.Where(r => r.EndCap && r.Openings.Length == 1).ToList(),
            kind);
    }

    private static List<Dungeons.Placed> Join(Dungeons.Kind kind, DungeonGen.Settings s, int seed,
        List<Dungeons.Room> entrances, List<Dungeons.Room> normal) =>
        (List<Dungeons.Placed>)PrivateCall.Static(typeof(DungeonGen), "JoinRooms", s, kind, new Random(seed), null, Vector3.Zero, entrances, normal)!;

    [Fact]
    public void ChooseRoomKindReturnsTheRequestedKindWhenItJoins()
    {
        var notes = new List<Said>();
        var kind = (Dungeons.Kind)PrivateCall.Static(typeof(DungeonGen), "ChooseRoomKind", new DungeonGen.Settings(Kind: "DG_ForestCrypt"), notes)!;
        Assert.Equal("DG_ForestCrypt", kind.Name);
        Assert.Empty(notes);
    }

    [Fact]
    public void ChooseRoomKindFallsBackToACaveForAKindTheGameLacks()
    {
        // A kind the game does not have is replaced by the cave (as the generator did before the split: no note is
        // made for it, a note being for a kind that has no rooms to join).
        var notes = new List<Said>();
        var kind = (Dungeons.Kind)PrivateCall.Static(typeof(DungeonGen), "ChooseRoomKind", new DungeonGen.Settings(Kind: "DG_NoSuchKind"), notes)!;
        Assert.Equal(StableHash.Of("DG_Cave"), kind.Hash);
    }

    [Fact]
    public void JoinRoomsStartsFromTheEntranceRoomChosenByTheSeed()
    {
        // The first room is the entrance the seed's first draw picks, placed at the origin.
        var (entrances, normal, _, kind) = RoomsOf("DG_ForestCrypt");
        const int Seed = 5;
        var rooms = Join(kind, new DungeonGen.Settings(Size: 1, Seed: Seed), Seed, entrances, normal);
        var expected = entrances[new Random(Seed).Next(entrances.Count)]; // nosemgrep
        Assert.Equal(expected.Hash, rooms[0].Hash);
        Assert.InRange(rooms.Count, 1, 8);
    }

    [Fact]
    public void CapOpenEndsAddsAnEndCapToEachEndItCanClose()
    {
        // Each open end gets a cap that fits it: the rooms grow when there are open ends to close.
        var (entrances, normal, caps, kind) = RoomsOf("DG_ForestCrypt");
        var rooms = Join(kind, new DungeonGen.Settings(Size: 1, Seed: 5), 5, entrances, normal);
        int before = rooms.Count;
        var notes = new List<Said>();
        PrivateCall.Static(typeof(DungeonGen), "CapOpenEnds", rooms, caps, new Random(5), notes);
        Assert.True(rooms.Count > before);
    }

    [Fact]
    public void CollectItemsOfNoRoomsIsEmpty()
    {
        var items = (List<DungeonGen.Item>)PrivateCall.Static(typeof(DungeonGen), "CollectItems", new List<Dungeons.Placed>(), null, Vector3.Zero, 1, new Random(1))!;
        Assert.Empty(items);
    }

    [Fact]
    public void CollectItemsOfJoinedRoomsPlacesTheGamesContents()
    {
        var (entrances, normal, _, kind) = RoomsOf("DG_ForestCrypt");
        var rooms = Join(kind, new DungeonGen.Settings(Size: 1, Seed: 5), 5, entrances, normal);
        var items = (List<DungeonGen.Item>)PrivateCall.Static(typeof(DungeonGen), "CollectItems", rooms, kind, Vector3.Zero, 1, new Random(5))!;
        Assert.NotEmpty(items);
    }
}
