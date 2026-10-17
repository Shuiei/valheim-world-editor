using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The foes a generated dungeon's settings name: one of a biome's (by its spawner or its creature), else taken as it
/// is, a Spawner_ name standing for the creature after the prefix.
/// </summary>
public class DungeonDresserFoeTests
{
    [Fact]
    public void AFoeIsFoundByItsSpawnerOrItsCreature()
    {
        Assert.Equal(new DungeonKit.Foe("Spawner_Draugr_Ranged", "Draugr_Ranged"), DungeonDresser.FoeOf("Spawner_Draugr_Ranged"));
        Assert.Equal(new DungeonKit.Foe("Spawner_Draugr_Ranged", "Draugr_Ranged"), DungeonDresser.FoeOf("Draugr_Ranged"));
        // A boss and an elite are foes too.
        Assert.Equal(new DungeonKit.Foe("Spawner_Troll", "Troll"), DungeonDresser.FoeOf("Troll"));
        Assert.Equal(new DungeonKit.Foe("Spawner_Greydwarf_Elite", "Greydwarf_Elite"), DungeonDresser.FoeOf("Greydwarf_Elite"));
    }

    [Fact]
    public void AFoeOfNoBiomeIsTakenAsItIs()
    {
        Assert.Equal(new DungeonKit.Foe("Spawner_Wolf", "Wolf"), DungeonDresser.FoeOf("Spawner_Wolf"));
        Assert.Equal(new DungeonKit.Foe("Wolf", "Wolf"), DungeonDresser.FoeOf("Wolf"));
    }
}
