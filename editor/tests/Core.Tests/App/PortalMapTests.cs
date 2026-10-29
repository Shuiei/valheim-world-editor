using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The map's portals: found by name (the session's added ones too, deleted ones left out), paired by the save's
/// connections first and then by tag two by two, the odd one alone.
/// </summary>
public class PortalMapTests
{
    /// <summary>
    /// A new wooden portal at a point with a tag, and the save's connection when given.
    /// </summary>
    private static NewObject Portal(int id, float x, float z, string tag, byte[]? connection = null)
    {
        var zdo = new ZdoData { Prefab = Fixtures.Hash("portal_wood"), Position = new Vector3(x, 10, z) };
        if (tag.Length > 0)
        {
            zdo.Set("strings", StableHash.Of("tag"), tag);
        }
        zdo.Connection = connection;
        return new NewObject(id, zdo.Prefab, zdo.Position, Vector3.Zero, 0f, null, false, zdo.Serialize());
    }

    [Fact]
    public void FindsAndPairsThePortals()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        int before = PortalMap.Find(world, edits).Portals.Count;
        // Three "home" portals: the save joins the first and the third; the second stays alone. Two "mine" portals
        // pair by tag, as do two without a tag; a lone "far" one has no partner.
        byte[] link = [0x01, 7, 0, 0, 0], target = [0x11, 7, 0, 0, 0];
        edits.AddObjects(
        [
            Portal(-1, 100, 0, "home", link),
            Portal(-2, 200, 0, "home"),
            Portal(-3, 300, 0, "home", target),
            Portal(-4, 400, 0, "mine"),
            Portal(-5, 500, 0, "mine"),
            Portal(-6, 600, 0, ""),
            Portal(-7, 700, 0, ""),
            Portal(-8, 800, 0, "far"),
        ]);
        var r = PortalMap.Find(world, edits);
        Assert.Equal(before + 8, r.Portals.Count);
        var mine = r.Portals.Where(p => p.Id < 0).ToList();
        Assert.All(mine, p => Assert.Equal("portal_wood", p.Name));
        Assert.Equal(["home", "home", "home", "mine", "mine", "", "", "far"], mine.Select(p => p.Tag));
        Assert.Equal(7, Assert.Single(mine, p => p.Id == -1).Link);
        Assert.Null(Assert.Single(mine, p => p.Id == -2).Link);
        var pairs = r.Links.Select(l => (r.Portals[l.A].Id, r.Portals[l.B].Id)).Where(l => l.Item1 < 0 || l.Item2 < 0)
            .Select(l => (Math.Max(l.Item1, l.Item2), Math.Min(l.Item1, l.Item2))).OrderByDescending(l => l.Item1).ToList();
        Assert.Equal([(-1, -3), (-4, -5), (-6, -7)], pairs);
        int Of(int id) => r.Portals.FindIndex(p => p.Id == id);
        Assert.True(r.IsLinked(Of(-1)));
        Assert.False(r.IsLinked(Of(-2)));
        Assert.False(r.IsLinked(Of(-8)));
        // Deleted: gone from the map; its partner is alone then.
        edits.SetDeleted([-5], true);
        var again = PortalMap.Find(world, edits);
        Assert.DoesNotContain(again.Portals, p => p.Id == -5);
        Assert.False(again.IsLinked(again.Portals.FindIndex(p => p.Id == -4)));
    }

    // A saved connection only counts while both ends still have the same tag: renamed, the ends pair by tag again.
    [Fact]
    public void ASavedLinkNeedsTheSameTagAtBothEnds()
    {
        var portals = new List<PortalMap.Portal>
        {
            new(1, "portal_wood", 0, 0, 0, "a", 5),
            new(2, "portal_wood", 1, 0, 0, "b", 5),
            new(3, "portal_wood", 2, 0, 0, "a", null),
            new(4, "portal_wood", 3, 0, 0, "b", null),
        };
        Assert.Equal([(0, 2), (1, 3)], PortalMap.Pair(portals));
        // Three ends of one connection: not a pair; by tag then.
        portals.Add(new(5, "portal_stone", 4, 0, 0, "a", 5));
        portals[2] = portals[2] with { Link = 5 };
        Assert.Equal([(0, 2), (1, 3)], PortalMap.Pair(portals));
    }

    [Fact]
    public void PortalsAreKnownByName()
    {
        Assert.True(PortalMap.IsPortal(Fixtures.Hash("portal_wood")));
        Assert.False(PortalMap.IsPortal(Fixtures.Hash("Beech1")));
        Assert.False(PortalMap.IsPortal(Fixtures.Hash("no_such_prefab_at_all")));
    }
}
