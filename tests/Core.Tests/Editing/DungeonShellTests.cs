using System.Globalization;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// DungeonShell's pillars and posts: segments from the foot up to the top, the last rising into the roof slab, never
/// past it into the room above. A doorway of no known kind: nothing. Its walls' windows: the same whatever the culture.
/// </summary>
public class DungeonShellTests
{
    [Theory]
    // Wood logs (4.44 m) in a tall room (10 m): a third would reach 13.32 m, 1.32 m into the room two levels up.
    [InlineData(4.44f, 10f, 2)]
    // In a room 4 m high, one rises 0.44 m into the slab.
    [InlineData(4.44f, 4f, 1)]
    // Stone pillars (2 m) reach the top exactly.
    [InlineData(2f, 10f, 5)]
    public void APillarRisesIntoTheRoofSlabNeverPastIt(float segment, float top, int count)
    {
        var o = new DungeonOut();

        DungeonShell.Stack(new DungeonKit.Column("pillar", segment, segment / 2), o, 0, 0, 0, top);

        Assert.Equal(count, o.Items.Count);
        Assert.All(o.Items, i => Assert.True(i.Position.Y + segment / 2 <= top + DungeonShell.RoofSlab + 0.01f));
    }

    [Fact]
    public void APillarOfSeveralSegmentsTakesTheTallestThatFits()
    {
        // The dresser's columns stacked their own way (DungeonDresser.Column); they now share Stack.
        var o = new DungeonOut();

        DungeonShell.Stack([new DungeonKit.Column("tall", 4, 2), new DungeonKit.Column("short", 2, 1)], o, 0, 0, 0, 7);

        Assert.Equal([("tall", 2f), ("short", 5f), ("short", 7f)], o.Items.Select(i => (i.Prefab, i.Position.Y)));
    }

    // A door of a kind the shell does not know (none the plan makes) adds nothing, and nothing fails.
    [Theory]
    [InlineData((DungeonKit.Door)(-1))]
    [InlineData((DungeonKit.Door)99)]
    public void ADoorOfNoKnownKindAddsNothing(DungeonKit.Door kind)
    {
        var o = new DungeonOut();
        var d = new DungeonPlan.Doorway { A = new(), B = new(), T1 = (0, 0), T2 = (0, 1), Dir = (1, 0), Kind = kind };

        DungeonShell.Doorway(d, 0, DungeonKit.MaterialOf("Stone"), o, "Meadows");

        Assert.Empty(o.Items);
    }

    // Each known door kind puts its own piece in its opening (the frame and what closes it are the shell's own).
    [Theory]
    [InlineData(DungeonKit.Door.Wood, "dungeon_forestcrypt_door")]
    [InlineData(DungeonKit.Door.Grand, "darkwood_gate")]
    [InlineData(DungeonKit.Door.Gate, "dungeon_sunkencrypt_irongate")]
    [InlineData(DungeonKit.Door.Key, "sunken_crypt_gate")]
    [InlineData(DungeonKit.Door.Grate, "iron_grate")]
    [InlineData(DungeonKit.Door.Curtain, "cloth_hanging_door")]
    [InlineData(DungeonKit.Door.Secret, "stone_wall_ruin_2")]
    public void EachDoorKindPutsItsPieceInTheOpening(DungeonKit.Door kind, string prefab)
    {
        var o = new DungeonOut();
        var d = new DungeonPlan.Doorway { A = new(), B = new(), T1 = (0, 0), T2 = (0, 1), Dir = (1, 0), Kind = kind };

        DungeonShell.Doorway(d, 0, DungeonKit.MaterialOf("Stone"), o, "Meadows");

        Assert.Contains(o.Items, i => i.Prefab == prefab);
    }

    [Fact]
    public void WindowsAreTheSameWhateverTheCulture()
    {
        // Where a wall gets a window is drawn from a hash of its place as text, which a comma-decimal culture wrote
        // otherwise ("1,5"): the same seed gave other windows.
        var grausten = DungeonKit.MaterialOf("Grausten");
        string Walls()
        {
            var o = new DungeonOut();
            DungeonShell.Fill(grausten, o, true, 1.5f, 0.5f, 80.5f, 0, 8);
            return string.Join(" ", o.Items.Select(i => i.Prefab));
        }
        string invariant = Walls();
        var was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            Assert.Equal(invariant, Walls());
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
        Assert.Contains(grausten.Window!, invariant);
    }
}
