using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// New pieces are player built: they get the chosen builder as "creator", like pieces built in the game. Opening worlds
/// sets the builder for new pieces (WorldSave.Builder): one world at a time.
/// </summary>
[Collection("World files")]
public class BuildersTests
{
    private static readonly int CreatorKey = StableHash.Of("creator");

    /// <summary>
    /// The "creator" an object holds (0: none).
    /// </summary>
    private static long CreatorOf(byte[] bytes) => ZdoData.Parse(bytes).LongList.FirstOrDefault(l => l.Key == CreatorKey).Value;

    [Fact]
    public void NewBuildPiecesGetTheBuilderOtherKindsAndMovesDoNot()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        long before = WorldSave.Builder;
        try
        {
            WorldSave.Builder = 1234567890123L;
            byte[] Source(ObjectRef m) => File.ReadAllBytes(Path.Combine(world.Directory, m.File.FileName));
            // Copied from one in the world when it has one, else built blank.
            byte[] Make(string name) => world.NewObjectBytes(new NewObject(-5, Fixtures.Hash(name), new Vector3(10, 30, 10), Vector3.Zero, 0f), Source)!;
            Assert.Equal(1234567890123L, CreatorOf(Make("woodwall")));        // hammer
            Assert.Equal(1234567890123L, CreatorOf(Make("sapling_turnip")));  // cultivator
            Assert.Equal(0L, CreatorOf(Make("Beech1")));                      // not built by players
                                                                              // Food set out with the serving tray is an item made a piece: marked so, it stays put.
            int PieceFlag(byte[] b) => ZdoData.Parse(b).IntList.FirstOrDefault(i => i.Key == StableHash.Of("piece")).Value;
            byte[] chicken = Make("HoneyGlazedChicken");
            Assert.Equal(1, PieceFlag(chicken));
            Assert.Equal(1234567890123L, CreatorOf(chicken));                 // feaster
            Assert.Equal(0, PieceFlag(Make("woodwall")));
            // A moved object keeps what it had.
            int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
            var z = ZdoData.Parse(world.ObjectBytes(chest));
            var moved = new NewObject(-6, z.Prefab, z.Position + new Vector3(1, 0, 0), z.Rotation, 0f, chest, false);
            Assert.Equal(0L, CreatorOf(world.NewObjectBytes(moved, Source)!));
        }
        finally
        {
            WorldSave.Builder = before;
        }
    }

    [Fact]
    public void GrownCropsAndTreesKeepTheirSaplingsGrowRadius()
    {
        Assert.Equal(0.5f, PrefabCatalog.GrownFrom["Pickable_Carrot"].Radius);
        Assert.Equal("Oak_Sapling", PrefabCatalog.GrownFrom["Oak1"].Sapling);
        Assert.False(PrefabCatalog.GrownFrom.ContainsKey("BlueberryBush"));
    }

    [Fact]
    public void ACharacterFileGivesItsNameAndPlayerId()
    {
        // ... per-world data, then the name, the player id and an empty start seed.
        byte[] name = System.Text.Encoding.UTF8.GetBytes("Shuiei");
        byte[] file = [1, 2, 3, 6, .. name, .. BitConverter.GetBytes(3349326647L), .. new byte[] { 0, 1, 0, 0 }];
        var c = Characters.Read(file, "shuiei");
        Assert.NotNull(c);
        Assert.Equal("Shuiei", c!.Name);
        Assert.Equal(3349326647L, c.Id);
        Assert.Null(Characters.Read(file, "someone"));
    }
}
