using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The four great hall variants, each dressed by hand in a clear room: the mead hall's fire, benches and tables; the hall
/// of the dead's sarcophagus and statues; the bare hall's broken altar; the colonnades' set piece at the far end.
/// </summary>
public class DungeonDresserHallVariantTests
{
    [Fact]
    public void MeadHallLightsItsFireAtTheMiddle()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, "mead", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        var fire = DungeonDresserSupport.At(o, room.Frame.W(0, -1, 0)).Select(i => i.Prefab).ToList();
        Assert.Contains(fire, p => p is "prop_bonfire" or "Morkhalla_firepit");
    }

    [Fact]
    public void MeadHallSeatsTwoBenchesAndTwoLogBenchesAroundItsFire()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, "mead", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.Equal(2, o.Items.Count(i => i.Prefab == "piece_bench01"));
        Assert.Equal(2, o.Items.Count(i => i.Prefab == "piece_logbench01"));
    }

    [Fact]
    public void HallOfTheDeadPutsItsSarcophagusAtTheFarEnd()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, "dead", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.Contains(DungeonDresserSupport.At(o, room.Frame.W(0, room.Frame.HalfD - 3, 0)), i => i.Prefab == "crypt_skeleton_chest");
    }

    [Fact]
    public void HallOfTheDeadStandsOneStatueEitherSideOfItsSarcophagus()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var f = room.Frame;
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(f, "dead", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.Single(DungeonDresserSupport.At(o, f.W(-2.3f, f.HalfD - 2.4f, 0)));
        Assert.Single(DungeonDresserSupport.At(o, f.W(2.3f, f.HalfD - 2.4f, 0)));
    }

    [Fact]
    public void BareHallLeavesItsFarAltarBroken()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, "bare", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.Contains(DungeonDresserSupport.At(o, room.Frame.W(0, room.Frame.HalfD - 2, 0)), i => i.Prefab == "StatueThor_broken_bottom");
    }

    [Fact]
    public void ColonnadesSetTheirFeatureAtTheFarEnd()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        var o = new DungeonOut();
        DungeonDresser.DressHallAs(room.Frame, "colonnades", room.Material, room.Biome, room.Style, room.Settings, new Random(1), o);
        Assert.NotEmpty(DungeonDresserSupport.At(o, room.Frame.W(0, room.Frame.HalfD - 2.2f, 0)));
    }

    [Fact]
    public void AHallVariantThatDoesNotExistIsRefused()
    {
        var room = DungeonDresserSupport.ClearRoom("Crypt");
        Assert.Throws<ArgumentException>(() => DungeonDresser.DressHallAs(room.Frame, "throne", room.Material, room.Biome, room.Style, room.Settings, new Random(1), new DungeonOut()));
    }
}
