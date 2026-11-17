using System.Globalization;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Region panel's objects handed to the Select tool and replaced there: Select objects narrowed to one kind of
/// object (one whose name holds a bracket too), Replace the selection on them, and a ship's root and parts replaced.
/// </summary>
[Collection("World files")]
public class AreaPanelReplaceTests
{
    [AvaloniaFact]
    public async Task SelectObjectsHandsThemOverAndTheSelectToolReplacesThem()
    {
        using var r = new SharedAreaPanelRun();
        r.P.Choose(AreaPanel.Act.Select);
        var names = SharedAreaPanelRun.Entries(r.P.OnlyBox);
        Assert.True(names.Count > 1, "the test world has objects of the chosen kinds in zone 0, 0");
        r.P.OnlyBox.SelectedIndex = 1;
        string name = names[1].Split(" (")[0];
        int count = int.Parse(names[1].Split(" (")[1].TrimEnd(')'), CultureInfo.InvariantCulture);
        await r.P.Apply();
        // The objects of that one kind, selected in the Select tool.
        Assert.Equal(ToolMode.Select, r.W.Tools.Mode);
        Assert.Equal($"Selected {count} object(s): Delete removes them, Replace the selection turns them into another kind.", r.Said);
        Assert.Equal(count, r.W.View.Selected.Count);
        Assert.All(r.W.View.Selected, i => Assert.Equal(name, r.W.NameOfPrefab(r.S.Scene.Things[i].Prefab)));
        // Replace the selection (the Select panel's): one undo step, as the panel's Replace made it.
        int other = r.S.Scene.World.Creatable.First(k => r.W.NameOfPrefab(k) is string n && n != name);
        r.W.SelectPanel.UsePicked(other);
        SharedButtons.Click(r.W.SelectPanel.ReplaceButton);
        Assert.StartsWith($"Replaced {count} object(s) with", r.Said);
        Assert.StartsWith($"Replaced {count} with", r.S.UndoLabel);
    }

    [AvaloniaFact]
    public async Task OnlyFindsAKindWhoseNameHoldsABracket()
    {
        // The name is the list's own, not cut out of its entry at " (": a mod's "Beech1 (mod)" would match nothing.
        using var r = new SharedAreaPanelRun();
        var p = new AreaPanel(r.W.View, () => r.S, k => (Terrain.PrefabCatalog.DisplayName(k) ?? k.ToString(CultureInfo.InvariantCulture)) + " (mod)", r.W.Tools.Brush);
        string said = "";
        p.Message += m => said = m.Shown;
        p.Choose(AreaPanel.Act.Select);
        p.Refresh();
        var names = SharedAreaPanelRun.Entries(p.OnlyBox);
        p.OnlyBox.SelectedIndex = 1;
        int cut = names[1].LastIndexOf(" (", StringComparison.Ordinal);
        Assert.EndsWith(" (mod)", names[1][..cut]);
        Assert.Equal(names[1][..cut], p.OnlyName());
        int count = int.Parse(names[1][(cut + 2)..^1], CultureInfo.InvariantCulture);
        await p.Apply();
        Assert.StartsWith($"Selected {count} object(s)", said);
        Assert.Equal(count, r.W.View.Selected.Count);
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
    public void TheReplaceWithListIsLeftAloneUntilTheWorldChangesAndKeepsItsChoice()
    {
        using var r = new SharedAreaPanelRun();
        var p = r.W.SelectPanel;
        // A kind the game places: every world can make it.
        int shared = Terrain.PrefabCatalog.Placeable.Select(i => StableHash.Of(i.Name)).First(h => r.W.NameOfPrefab(h) != null);
        p.UsePicked(shared);
        Assert.Equal(shared, p.ReplaceKinds[p.ReplaceBox.SelectedIndex]);
        // The same world, nothing new in it: switching to the Select tool leaves the list as it is.
        var before = p.ReplaceBox.ItemsSource;
        r.W.Tools.ChooseSelect();
        Assert.Same(before, p.ReplaceBox.ItemsSource);
        // Another session opened: worked out again for its world, the kind chosen still chosen.
        var scene = WorldScene.Load(r.Dir, 0, 0, 1);
        r.W.View.Show(scene, null);
        r.W.Edit(scene.Session!);
        Assert.NotSame(before, p.ReplaceBox.ItemsSource);
        Assert.Equal(shared, p.ReplaceKinds[p.ReplaceBox.SelectedIndex]);
    }
}
