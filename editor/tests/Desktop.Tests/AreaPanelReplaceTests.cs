using System.Globalization;
using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's Replace action: the two lists chosen and applied, a kind whose name holds a bracket, a ship's root and parts replaced, and the With list worked out again for another world.
/// </summary>
[Collection("World files")]
public class AreaPanelReplaceTests
{
    [AvaloniaFact]
    public async Task ReplaceUsesTheTwoLists()
    {
        using var r = new SharedAreaPanelRun();
        await r.ReplaceWithNothingChosen();
        Assert.Equal("Choose what to replace, and with what.", r.Said);
        var from = ((IEnumerable<object>)r.P.FromBox.ItemsSource!).Cast<string>().ToList();
        Assert.NotEmpty(from);
        r.P.FromBox.SelectedIndex = 0;
        string name = from[0].Split(" (")[0];
        int count = int.Parse(from[0].Split(" (")[1].TrimEnd(')'), CultureInfo.InvariantCulture);
        var to = ((IEnumerable<object>)r.P.ToBox.ItemsSource!).Cast<string>().ToList();
        r.P.ToBox.SelectedIndex = to.FindIndex(t => t != name);
        await r.P.Apply();
        Assert.StartsWith($"Replaced {count} object(s) with", r.Said);
        Assert.StartsWith($"Replaced {count} with", r.S.UndoLabel);
    }

    [AvaloniaFact]
    public async Task ReplaceFindsAKindWhoseNameHoldsABracket()
    {
        // The kind was cut out of the list's entry at " (", so a mod's "Beech1 (mod)" matched nothing: 0 replaced.
        using var r = new SharedAreaPanelRun();
        var p = new AreaPanel(r.W.View, () => r.S, k => (Terrain.PrefabCatalog.DisplayName(k) ?? k.ToString(CultureInfo.InvariantCulture)) + " (mod)");
        string said = "";
        p.Message += m => said = m.Shown;
        p.Choose(AreaPanel.Act.Replace);
        p.Refresh();
        var from = ((IEnumerable<object>)p.FromBox.ItemsSource!).Cast<string>().ToList();
        p.FromBox.SelectedIndex = 0;
        int cut = from[0].LastIndexOf(" (", StringComparison.Ordinal);
        string kind = from[0][..cut];
        int count = int.Parse(from[0][(cut + 2)..^1], CultureInfo.InvariantCulture);
        var to = ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>().ToList();
        p.ToBox.SelectedIndex = to.FindIndex(t => t != kind);
        await p.Apply();
        Assert.StartsWith($"Replaced {count} object(s) with", said);
    }

    [AvaloniaFact]
    public void ReplacingAShipsRootTakesItsPartsAwayAndAPartGoesAlone()
    {
        using var r = new SharedAreaPanelRun();
        var ship = TestScenes.AddShip(r.S, 0, 0);
        int beech = StableHash.Of("Beech1");
        int beeches = r.S.Scene.Things.Count(t => !t.Gone && t.Prefab == beech);
        r.P.Replace(r.S, [ship[0]], beech);
        Assert.StartsWith("Replaced 1 object(s) with ", r.Said);
        Assert.EndsWith(". Their 4 linked part(s) went with them.", r.Said);
        Assert.All(ship, i => Assert.True(r.S.Scene.Things[i].Gone));
        Assert.Equal(beech, r.S.Scene.Things[^1].Prefab);
        Assert.Equal(beeches + 1, r.S.Scene.Things.Count(t => !t.Gone && t.Prefab == beech));
        // One undo step brings the whole ship back. The root and a part given together: the four parts are counted.
        r.S.Undo();
        Assert.All(ship, i => Assert.False(r.S.Scene.Things[i].Gone));
        r.P.Replace(r.S, [ship[0], ship[1]], beech);
        Assert.StartsWith("Replaced 2 object(s) with ", r.Said);
        Assert.EndsWith(". Their 4 linked part(s) went with them.", r.Said);
        Assert.All(ship, i => Assert.True(r.S.Scene.Things[i].Gone));
        // A part replaced alone leaves the rest of the ship.
        r.S.Undo();
        r.P.Replace(r.S, [ship[1]], beech);
        Assert.DoesNotContain("linked part", r.Said);
        Assert.Equal([ship[1]], ship.Where(i => r.S.Scene.Things[i].Gone));
    }

    [AvaloniaFact]
    public void TheWithListIsWorkedOutAgainForAnotherWorld()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // Two worlds, each with a kind of its own to copy (named here: the game's catalogue does not place them); the
        // first is the live snapshot, for a live merge to bring it a kind.
        int first = StableHash.Of("mod_first"), second = StableHash.Of("mod_second"), live = StableHash.Of("mod_live");
        string? Name(int h) => h == first ? "mod_first" : h == second ? "mod_second" : h == live ? "mod_live" : Terrain.PrefabCatalog.DisplayName(h);
        var p = new AreaPanel(w.View, () => w.Session, Name);
        var a = TestScenes.Flat(1, new WorldScene.Thing(first, first, new Vector3(10, 30, 10), Vector3.Zero, 0, true));
        a.Scene.World = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        a.Scene.World.Templates[first] = 0;
        var b = TestScenes.Flat(1, new WorldScene.Thing(second, second, new Vector3(10, 30, 10), Vector3.Zero, 0, true));
        b.Scene.World.Templates[second] = 0;
        w.View.Show(a.Scene, null);
        w.Edit(a);
        p.UsePicked(first);
        var with = ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>().ToList();
        Assert.Contains("mod_first", with);
        Assert.DoesNotContain("mod_second", with);
        Assert.DoesNotContain("mod_live", with);
        Assert.Equal("mod_first", p.ToBox.SelectedItem);
        // The game brings the same world a kind it had none of: offered after the next refresh, the choice kept.
        Assert.NotNull(a.Scene.World.MergeLive([(10, 10)], [(4242, 1, MadeObjects.Piece("mod_live", new Vector3(640, 30, 640), 777, "Astrid"))], _ => false, _ => false));
        p.Refresh();
        with = [.. ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>()];
        Assert.Contains("mod_live", with);
        Assert.Equal("mod_first", p.ToBox.SelectedItem);
        // A kind both worlds have (one the game places) stays chosen in the other world; the first world's own kinds
        // are not offered there.
        int shared = Terrain.PrefabCatalog.Placeable.Select(i => StableHash.Of(i.Name)).First(h => Name(h) != null);
        p.UsePicked(shared);
        w.View.Show(b.Scene, null);
        w.Edit(b);
        p.Refresh();
        with = [.. ((IEnumerable<object>)p.ToBox.ItemsSource!).Cast<string>()];
        Assert.DoesNotContain("mod_first", with);
        Assert.DoesNotContain("mod_live", with);
        Assert.Contains("mod_second", with);
        Assert.Equal(Terrain.PrefabCatalog.DisplayName(shared), p.ToBox.SelectedItem);
        p.UsePicked(second);
        Assert.Equal("mod_second", p.ToBox.SelectedItem);
        w.View.Show(a.Scene, null);
        w.Edit(a);
        p.Refresh();
        Assert.Equal(-1, p.ToBox.SelectedIndex);
        // No world open: the list is empty and the last world is let go of.
        w.View.Show(TestScenes.FlatScene(), null);
        p.Refresh();
        Assert.Empty((IEnumerable<object>)p.ToBox.ItemsSource!);
    }
}
