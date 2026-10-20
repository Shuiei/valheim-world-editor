using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using ValheimGen;
using Xunit;
using Vector3 = System.Numerics.Vector3;

namespace WorldEditor.Tests;

/// <summary>
/// Expand World Data's room and dungeon files laid over the game's catalogue (Dungeons.Scope / UseMod): rooms left out
/// still drawn but unused, a copy of a game room with its own theme and objects and saved as its game room, openings
/// changed, exit-only and added, a generator's themes, box, doors, counts, excluded and required rooms and limits.
/// </summary>
public class DungeonsModTests
{
    private static readonly Dungeons.Kind Cave = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;

    private static RoomEntry Room(string name, string theme = "Cave", IReadOnlyList<RoomConnection>? connections = null, IReadOnlyList<ContentObject>? objects = null) =>
        new(name, theme, true, false, false, false, 0, 1f, false, "12,12,12", connections ?? [], objects ?? []);

    private static DungeonEntry Dungeon(string themes = "Cave", string bounds = "", int min = 1, int max = 1, IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? excluded = null, IReadOnlyList<RoomLimit>? limits = null, IReadOnlyList<DoorEntry>? doors = null) =>
        new("DG_Cave", "Dungeon", bounds, themes, min, max, required ?? [], excluded ?? [], limits ?? [], doors ?? [], 0.4f, false, true);

    [Fact]
    public void RoomsTheFileLeavesOutAreStillDrawnButNeverUsed()
    {
        using (Dungeons.Scope(new DungeonRules { Rooms = [Room("cave_new_corridor01")] }))
        {
            Assert.Equal(["cave_new_corridor01"], Dungeons.RoomsFor(Dungeons.KindOf(Cave.Hash)!).Select(r => r.Name));
            Assert.False(Dungeons.RoomOf("cave_new_corridor05")!.Enabled);
        }
        // Out of the scope: the game's again.
        Assert.True(Dungeons.RoomsFor(Cave).Count() > 1);
    }

    [Fact]
    public void ACopyIsARoomOfItsOwnBuiltAsItsGameRoom()
    {
        var mushroom = new ContentObject("GlowingMushroom", new Vector3(1, 0.5f, 2), Vector3.Zero, Vector3.One, null, 1f, false);
        var never = mushroom with { Prefab = "Rock_3", Chance = 0f };
        using var _ = Dungeons.Scope(new DungeonRules { Rooms = [Room("cave_new_corridor01"), Room("cave_new_corridor01:Lit", "Cave, Glow", objects: [mushroom, never])] });
        var game = Dungeons.RoomOf("cave_new_corridor01")!;
        var copy = Dungeons.RoomOf("cave_new_corridor01:Lit")!;
        Assert.Equal(("cave_new_corridor01", StableHash.Of("cave_new_corridor01:Lit")), (copy.Game, copy.Hash));
        // A theme the game does not have takes the flag past its highest (MorkHalla, 131072).
        Assert.Equal(4 + 262144, copy.Theme);
        Assert.Equal(game.Openings, copy.Openings);
        Assert.Equal(game.Contents, copy.Contents);
        // Its objects with it, each by its chance; saved as its game room, as the mod saves it.
        var placed = new Dungeons.Placed(copy.Hash, new Vector3(100, 5000, 40), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        var made = Dungeons.Contents(placed, Dungeons.KindOf(Cave.Hash)!, placed.Position, 1);
        var added = Assert.Single(made, m => m.Prefab == "GlowingMushroom");
        Assert.Equal(placed.ToWorld(mushroom.Position), added.Position);
        Assert.DoesNotContain(made, m => m.Prefab == "Rock_3");
        Assert.Equal("cave_new_corridor01", Assert.Single(Dungeons.Read(Dungeons.Write([placed]))).Name);
    }

    [Fact]
    public void TheFilesSwapAndGiveDataToARoomsObjects()
    {
        List<ContentField> marked = [new("ints", StableHash.Of("mark"), "1")], own = [new("ints", StableHash.Of("own"), "2")];
        var placed = new Dungeons.Placed(StableHash.Of("cave_new_corridor01"), new Vector3(100, 5000, 40), Quaternion.Identity);
        var plain = Dungeons.Contents(placed, Cave, placed.Position, 1);
        var room = Room("cave_new_corridor01", objects: [new ContentObject("GlowingMushroom", new Vector3(1, 0, 2), Vector3.Zero, Vector3.One, own, 1f, false)]) with
        {
            // The room's swaps come after the dungeon's: stalagmites become stalactites, then rocks; crystals go.
            Swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { ["caverock_ice_stalagtite"] = [new(1f, "Rock_3")], ["Pickable_MountainCaveCrystal"] = [new(1f, "")] },
            Data = new Dictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> { ["all"] = [new(1f, marked)] },
        };
        var dungeon = Dungeon() with { Swaps = new Dictionary<string, IReadOnlyList<Weighted<string>>> { ["caverock_ice_stalagmite"] = [new(1f, "caverock_ice_stalagtite")] } };
        using var _ = Dungeons.Scope(new DungeonRules { Rooms = [room], Dungeons = [dungeon] });
        var made = Dungeons.Contents(placed, Dungeons.KindOf(Cave.Hash)!, placed.Position, 1);
        var rocks = plain.Where(m => m.Prefab != "Pickable_MountainCaveCrystal").ToList();
        Assert.NotEmpty(rocks);
        Assert.Equal(rocks.Select(m => ("Rock_3", m.Position)), made.SkipLast(1).Select(m => (m.Prefab, m.Position)));
        Assert.All(made.SkipLast(1), m => Assert.Equal(marked, m.Data!));
        // The added object: the file's data for all, then its own.
        Assert.Equal("GlowingMushroom", made[^1].Prefab);
        Assert.Equal([.. marked, .. own], made[^1].Data!);
    }

    [Fact]
    public void TheFilesOpeningsChangeTheGameRoomsAddToThemAndCanBeExitOnly()
    {
        RoomConnection[] connections =
        [
            new("1, 2, 3, 90, 0, 0", "lit", false, true, "other"),
            new("", "", true, false, "true"),
            new("4, 5, 6", "new", false, false, "false"),
        ];
        var game = Dungeons.RoomOf("cave_new_corridor01")!;
        using var _ = Dungeons.Scope(new DungeonRules { Rooms = [Room("cave_new_corridor01", connections: connections)] });
        var room = Dungeons.RoomOf("cave_new_corridor01")!;
        Assert.Equal(3, room.Openings.Length);
        var lit = room.Openings[0];
        Assert.Equal(new Vector3(1, 3, 2), lit.Position);
        Assert.Equal(BlueprintFormats.FromEuler(new Vector3(0, 90, 0)), lit.Rotation);
        Assert.Equal(("lit", true, true, false), (lit.Type, lit.ExitOnly, lit.OnlyIfOther, lit.AllowDoor));
        // Exit-only: never what joins the room to another.
        Assert.Empty(Dungeons.Matching(room, "lit"));
        // No place given: the game room's opening's.
        Assert.Equal((game.Openings[1].Position, true, true), (room.Openings[1].Position, room.Openings[1].Entrance, room.Openings[1].AllowDoor));
        Assert.Equal(("new", new Vector3(4, 6, 5), false), (room.Openings[2].Type, room.Openings[2].Position, room.Openings[2].AllowDoor));
    }

    [Theory]
    [InlineData("", 64f, 256f, 64f)]
    [InlineData("96", 96f, 96f, 96f)]
    [InlineData("100, 80, 50", 100f, 50f, 80f)]
    [InlineData("100, 80", 100f, 256f, 80f)]
    public void ADungeonsBoxIsReadAsTheModReadsIt(string bounds, float x, float y, float z)
    {
        using var _ = Dungeons.Scope(new DungeonRules { Dungeons = [Dungeon(bounds: bounds)] });
        Assert.Equal(new Vector3(x, y, z), Dungeons.KindOf(Cave.Hash)!.ZoneSize);
    }

    [Fact]
    public void ADungeonTakesTheFilesSettings()
    {
        DoorEntry[] doors = [new("cloth_hanging_door_double", "shrine", 0.8f), new("NoSuchDoor", "", 1f)];
        using var _ = Dungeons.Scope(new DungeonRules
        {
            Dungeons = [Dungeon("Cave, Unknown", min: 20, max: 30, required: ["CAVE_NEW_CORRIDOR09"], excluded: ["Cave_New_Corridor01"], doors: doors)],
        });
        var kind = Dungeons.KindOf(Cave.Hash)!;
        Assert.Equal((4, true, (20, 30), 0.4f), (kind.Themes, kind.BaseSeed, kind.Rooms!.Value, kind.DoorChance));
        // A door of no prefab is left out; room names as the game writes them.
        Assert.Equal([("cloth_hanging_door_double", "shrine", 0.8f)], kind.Doors);
        Assert.Equal(["cave_new_corridor09"], kind.Required);
        Assert.DoesNotContain(Dungeons.RoomsFor(kind), r => r.Name == "cave_new_corridor01");
        // The other kinds as the game has them.
        Assert.Equal(Dungeons.KindOf(StableHash.Of("DG_SunkenCrypt")), Dungeons.Kinds.First(k => k.Name == "DG_SunkenCrypt"));
    }

    [Fact]
    public void TheGeneratorFollowsTheCountsRequiredRoomsAndLimits()
    {
        RoomLimit[] limits = [new("cave_new_corridor05", 0, 1, 0), new("cave_new_crossroads02", 0, 0, 2)];
        using var _ = Dungeons.Scope(new DungeonRules { Dungeons = [Dungeon(bounds: "192", min: 20, max: 25, required: ["cave_new_corridor09"], limits: limits)] });
        var r = DungeonGen.Make(new DungeonGen.Settings(Made: DungeonGen.Made.Rooms, Kind: "DG_Cave", Size: 1, Seed: 11));
        var joined = r.Rooms!.Where(p => !p.Room!.EndCap).ToList();
        Assert.True(joined.Count >= 20, $"{joined.Count} rooms joined");
        Assert.True(joined.Count(p => p.Room!.Game == "cave_new_corridor05") <= 1);
        Assert.Contains(joined, p => p.Room!.Game == "cave_new_corridor09");
        Assert.True(joined.Count(p => p.Room!.Game == "cave_new_crossroads02") >= 2);
    }
}
