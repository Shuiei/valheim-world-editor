using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Where the dressing puts a room's fixtures: the antechamber's warning hangs on the wall's inner face, and a shrine's stone stands on its plinth.
/// </summary>
public class DungeonDresserFixturesTests
{
    [Fact]
    public void TheAntechambersWarningHangsOnTheWallsInnerFace()
    {
        // The warning beside the boss's locked gate was put 4 cm from the doorway's middle, which is on the wall's centre
        // line: 0.54 m deep in a stone wall. It belongs on the antechamber's side, on the wall's inner face.
        int signs = 0;
        foreach (string biome in new[] { "Black Forest", "Mistlands" })
        {
            float thick = DungeonKit.MaterialOf(DungeonKit.BiomeOf(biome).Material).Thick;
            for (int seed = 1; seed <= 3; seed++)
            {
                var r = DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: "Crypt", Size: 2, Levels: 1, Seed: seed));
                foreach (var sign in r.Items.Where(i => i.Prefab == "sign" && i.Data != null && i.Data.Any(d => d.Value.StartsWith("Sealed", StringComparison.Ordinal))))
                {
                    signs++;
                    var ante = r.Map!.Single(m => m.Name == "Antechamber");
                    var gate = r.Doors!.Where(d => d.Kind == "Key").MinBy(d => Vector2.Distance(new(d.X, d.Z), new(sign.Position.X, sign.Position.Z)))!;
                    float dx = MathF.Abs(sign.Position.X - gate.X), dz = MathF.Abs(sign.Position.Z - gate.Z);
                    Assert.Equal(2.6f, MathF.Max(dx, dz), 0.01f);
                    Assert.Equal(thick / 2 + 0.04f, MathF.Min(dx, dz), 0.01f);
                    Assert.True(sign.Position.X > ante.X0 && sign.Position.X < ante.X1 && sign.Position.Z > ante.Z0 && sign.Position.Z < ante.Z1,
                        $"{biome}, seed {seed}: the warning at {sign.Position} is not on the antechamber's side");
                }
            }
        }
        Assert.True(signs > 0, "no warning by a locked gate");
    }

    [Fact]
    public void AShrinesStoneStandsOnItsPlinthWhicheverIsPicked()
    {
        // The memorial stone was lifted by the medium one's foot whichever was picked: the large one sank 0.13 m.
        var bases = new List<(string Prefab, float Base)>();
        for (int seed = 1; seed <= 8; seed++)
        {
            var r = DungeonGen.Make(new DungeonGen.Settings(Biome: "Mountain", Style: "Temple", Size: 3, Levels: 1, Seed: seed));
            foreach (var i in r.Items.Where(i => i.Prefab.StartsWith("MemorialStone_", StringComparison.Ordinal)))
            {
                var shrine = r.Map!.Where(m => m.Name == "Shrine").SingleOrDefault(m => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1);
                if (shrine != null)
                {
                    bases.Add((i.Prefab, i.Position.Y - DungeonProps.Foot(i.Prefab) - shrine.Y));
                }
            }
        }
        Assert.Contains(bases, b => b.Prefab == "MemorialStone_Large");
        Assert.All(bases, b => Assert.Equal(bases[0].Base, b.Base, 0.001f));
    }

    [Fact]
    public void ADolmenIsInItsRoomAboveTheFloor()
    {
        // The dolmen (a shrine on a dais, outside the mountain and the mistlands) stands in a room, above its floor.
        int dolmens = 0;
        foreach (string biome in new[] { "Black Forest", "Plains", "Swamp" })
        {
            foreach (string style in new[] { "Temple", "Crypt" })
            {
                for (int size = 2; size <= 3; size++)
                {
                    for (int seed = 1; seed <= 20; seed++)
                    {
                        var r = DungeonGen.Make(new DungeonGen.Settings(Biome: biome, Style: style, Size: size, Levels: 1, Seed: seed));
                        foreach (var i in r.Items.Where(i => i.Prefab.StartsWith("RockDolmen_", StringComparison.Ordinal)))
                        {
                            dolmens++;
                            var room = r.Map!.SingleOrDefault(m => i.Position.X > m.X0 && i.Position.X < m.X1 && i.Position.Z > m.Z0 && i.Position.Z < m.Z1);
                            Assert.True(room != null, $"{biome} {style} {size} {seed}: the dolmen at {i.Position} is in no room");
                            Assert.True(i.Position.Y > room!.Y, $"{biome} {style} {size} {seed}: the dolmen at {i.Position} is not above its room's floor {room.Y}");
                        }
                    }
                }
            }
        }
        Assert.True(dolmens > 0, "no dolmen was made in any of the dungeons tried");
    }
}
