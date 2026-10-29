using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A script's changes going into the area (ScriptHost.Apply.cs): a part of a linked group (the example vehicle mod's
/// ship, editor/tests/Support/ExampleMod) taken away or moved brings its whole group, as the Select tool and Claude do.
/// </summary>
public class ScriptHostApplyTests
{
    // Objects.Remove of one floor of a ship took that floor alone, leaving the ship without it.
    [Fact]
    public void RemovingAPartTakesItsWholeGroup()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        var ch = new ScriptHost.Changes();
        ch.Remove.Add(ship[1]);
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.All(ship, i => Assert.True(s.Scene.Things[i].Gone));
        Assert.True(s.Undo());
        Assert.All(ship, i => Assert.False(s.Scene.Things[i].Gone));
    }

    // Objects.Move of one floor of a ship moved that floor alone; the whole ship moves with it now, in its shape.
    [Fact]
    public void MovingAPartMovesItsWholeGroup()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        var was = ship.Select(i => s.Scene.Things[i].Position).ToList();
        var floor = s.Scene.Things[ship[1]];
        var ch = new ScriptHost.Changes();
        ch.Moves[ship[1]] = (floor.Position + new Vector3(5, 0, 0), floor.Rotation, floor.Scale);
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.All(ship, i => Assert.True(s.Scene.Things[i].Gone));
        var now = s.Scene.Things.Skip(ship.Max() + 1).Where(t => !t.Gone).Select(t => t.Position).ToList();
        Assert.Equal(ship.Count, now.Count);
        Assert.All(was, p => Assert.Contains(now, q => Vector3.Distance(p + new Vector3(5, 0, 0), q) < 1e-3f));
    }

    // A part both moved and taken away is only taken away, with its group.
    [Fact]
    public void APartMovedAndTakenAwayIsOnlyTakenAway()
    {
        var s = TestScenes.Flat(2);
        var ship = TestScenes.AddShip(s);
        int count = s.Scene.Things.Count;
        var floor = s.Scene.Things[ship[2]];
        var ch = new ScriptHost.Changes();
        ch.Remove.Add(ship[1]);
        ch.Moves[ship[2]] = (floor.Position + new Vector3(5, 0, 0), floor.Rotation, floor.Scale);
        ScriptHost.Apply(s, ch, "Script: test");
        Assert.All(ship, i => Assert.True(s.Scene.Things[i].Gone));
        Assert.Equal(count, s.Scene.Things.Count);
    }
}
