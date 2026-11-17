using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's camera: into the dungeon, back to its entrance, and the view's cut counted from the dungeon's floor.
/// </summary>
public sealed partial class DungeonPanel
{
    /// <summary>
    /// The camera back down to the dungeon's entrance on the ground: the dungeon location in its zone (the game lays a
    /// dungeon out over its entrance's zone), else the ground straight under it. Nothing cut away.
    /// </summary>
    internal void Outside()
    {
        if (Tool.Dungeon is not { } d || _view.Scene is not { } s)
        {
            return;
        }
        var p = d.Thing.Position;
        var zone = DungeonEntrances.ZoneCentre(p);
        var entrance = LocationParts.Of(s).FirstOrDefault(l => DungeonGen.DungeonLocations.Contains(l.Location) && DungeonEntrances.ZoneCentre(l.Position) == zone);
        float x = entrance.Location != 0 ? entrance.Position.X : p.X, z = entrance.Location != 0 ? entrance.Position.Z : p.Z;
        ShowRoofs();
        _view.Orbit(x, z, _view.Camera.Yaw * 180f / MathF.PI, 55, 40);
    }

    /// <summary>The cut off: the roofs shown (the view's cut slider at its right end).</summary>
    internal void ShowRoofs() => _cut.Off();

    /// <summary>
    /// The camera to the middle of the dungeon's rooms (its dungeon object when it has none), looking down into it;
    /// the cut on (4 m when off).
    /// </summary>
    internal void Inside()
    {
        if (Tool.Dungeon is not { } d)
        {
            return;
        }
        var p = d.Rooms.Count > 0 ? d.Rooms.Aggregate(System.Numerics.Vector3.Zero, (a, r) => a + r.Position) / d.Rooms.Count : d.Thing.Position;
        _view.Orbit(p.X, p.Z, 30, 55, 70, p.Y);
        CutFromFloor();
        if (!_cut.On)
        {
            _cut.Set(InsideCut);
        }
    }

    /// <summary>The cut Go to the dungeon puts on when there is none: 4 m above the floor shows the rooms' insides.</summary>
    internal const double InsideCut = 4;

    /// <summary>
    /// The view's cut counts from the open dungeon's floor: about the floor of its entrance (3 m under the dungeon
    /// object, as in the game's caves). Coming from the world, the cut is the one the dungeon had last (off at first);
    /// changing dungeon, it keeps its metres. Without a dungeon open, it counts from the ground, as in the world.
    /// </summary>
    private void CutFromFloor()
    {
        if (Tool.Dungeon is { } d)
        {
            _cut.FromDungeon(d.Thing.Position.Y - 3);
        }
        else
        {
            _cut.FromGround();
        }
    }
}
