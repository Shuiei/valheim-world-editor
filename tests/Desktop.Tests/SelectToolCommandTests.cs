using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Select tool's keys and commands: Same kind, Drop onto what is under the objects or the ground, lifting and
/// turning with the keys, and Delete with nothing selected.
/// </summary>
[Collection("World files")]
public class SelectToolCommandTests
{
    [AvaloniaFact]
    public void SameKindSelectsEveryShownObjectOfTheSelectedKinds()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 30, 50, false), T(3, Wall, 45, 30, 45));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Tool.SameKind();
        Assert.StartsWith("Select an object of each kind", said);
        v.W.View.Select([0]);
        v.Tool.SameKind();
        Assert.Equal([0, 1], v.Selected);
        // Hidden kinds are left out.
        v.W.View.SetShown(ObjectKinds.Of("Beech1", false), false);
        v.W.View.Select([0, 2]);
        v.Tool.SameKind();
        Assert.Equal([2], v.Selected);
    }

    [AvaloniaFact]
    public void DropPutsObjectsOnTheGroundOrOnWhatIsUnderThem()
    {
        // A tree floating 6 m up, and another above a floor.
        var v = Open(T(1, Beech, 40, 36, 40, false), T(2, Floor, 50, 31, 50), T(3, Beech, 50, 40, 50, false));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.W.View.Select([0, 2]);
        Assert.True(v.Tool.Key(Avalonia.Input.Key.End, false));
        Assert.Equal("Dropped 2 object(s): 1 onto other objects, 1 onto the ground.", said);
        v.Tool.Commit();
        var trees = v.S.Scene.Things.Where(t => !t.Gone && t.Prefab == Beech).OrderBy(t => t.Position.X).ToList();
        Assert.Equal(30, trees[0].Position.Y, 2);
        Assert.Equal(31 + PieceCatalog.Get(Floor)!.MaxY, trees[1].Position.Y, 2);
    }

    [AvaloniaFact]
    public void KeysLiftAndTurnTheSelection()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        v.W.View.Select([0]);
        v.Tool.OnGround = false;
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, true));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.PageDown, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.OemPeriod, false));
        Assert.True(v.Tool.Key(Avalonia.Input.Key.OemComma, true));
        Assert.False(v.Tool.Key(Avalonia.Input.Key.Q, false));
        v.Tool.Commit();
        // +0.25 + 1 - 0.25 = +1 m; +1° then -15°.
        Assert.Equal(31, v.Last.Position.Y, 2);
        Assert.Equal(346, (v.Last.Rotation.Y % 360 + 360) % 360, 1);
    }

    [AvaloniaFact]
    public void DeleteWithNothingSelectedSaysSo()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false));
        string? said = null;
        v.Tool.Message += (t, _) => said = t.Shown;
        v.Tool.Delete();
        Assert.Equal("Select objects to delete first.", said);
        Assert.False(v.S.CanUndo);
    }
}
