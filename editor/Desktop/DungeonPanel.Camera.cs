using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The panel's camera: into the dungeon, back to its entrance, and the roof cut.
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

    /// <summary>The cut off: the roofs shown (the slider at its right end).</summary>
    internal void ShowRoofs()
    {
        CutSlider.Value = CutOff;
        SetCut(CutOff);
    }

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
        if (CutSlider.Value >= CutOff)
        {
            CutSlider.Value = 4;
        }
        SetCut(CutSlider.Value);
    }

    /// <summary>
    /// Cuts the view's roofs away above a height: from about the floor of the dungeon's entrance (3 m under the
    /// dungeon object); <see cref="CutOff"/> metres: off.
    /// </summary>
    private void SetCut(double metres)
    {
        // From about the floor of the dungeon's entrance (3 m under the dungeon object, as in the game's caves).
        float? y = metres < CutOff && Tool.Dungeon is { } d ? d.Thing.Position.Y - 3 + (float)metres : null;
        _view.CutY = y;
        _cutText.Text = metres < CutOff ? Lang.T("dungeon.label.cut", metres) : Lang.T("dungeon.label.cutOff");
    }
}
