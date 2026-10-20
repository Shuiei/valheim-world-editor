using System.Numerics;
using Avalonia;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The left button went down: drags a handle, starts a move of the selection, starts a zone on empty ground,
    /// or picks.
    /// </summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="shift">Shift held: add to the selection.</param>
    /// <param name="alt">Alt held: objects are not hit (the ground only).</param>
    /// <param name="clicks">The click count (2 or more on a building piece other than a chest: the whole building).</param>
    public void Down(Point at, Size size, bool shift, bool alt, int clicks)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        if (PressHandle(at, size))
        {
            return;
        }
        int? hit = alt ? null : _view.ObjectAt(at, size);
        var ground = _view.WorldAt(at, size);
        // A double click on a linked group's part (a ship): the whole group; on a building piece: the whole building;
        // on a chest, the chest alone (its inspector opens: GlView.InspectAsked).
        if (clicks >= 2 && hit is int dbl && !(PrefabCatalog.Details(s.Things[dbl].Prefab)?.ContainerW > 0))
        {
            if (Session?.WithLinked([dbl]) is { Groups: > 0 } v)
            {
                _view.Select(v.Indices);
                Message?.Invoke(Said.Of("select.msg.wholeLinked", Noun(v.Noun), v.Indices.Count), false);
                return;
            }
            if (IsPiece(s.Things[dbl]))
            {
                WholeBuilding([dbl]);
                return;
            }
        }
        if (hit is int h && _view.Selected.Contains(h) && !shift && ground is { } g)
        {
            _drag = (at, g);
            _dragMoving = false;
            return;
        }
        Commit();
        if (hit == null && StartZone(at, ground, shift))
        {
            return;
        }
        _view.Pick(at, size, shift, Range);
    }

    /// <summary>
    /// A press on empty ground (or with Alt held): a click deselects, a drag draws a rectangle on the screen
    /// (<see cref="SeenOnly"/>) or a zone on the ground. False when there is no ground under the pointer for a zone.
    /// </summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="ground">The ground under it (world), or null.</param>
    /// <param name="shift">Shift held: add to the selection.</param>
    private bool StartZone(Point at, Vector3? ground, bool shift)
    {
        if (SeenOnly)
        {
            _box = (at, at, shift, false);
            return true;
        }
        if (ground is not { } g)
        {
            return false;
        }
        _lasso = (at, new List<Vector2> { new(g.X, g.Z) }, shift, false);
        return true;
    }
}
