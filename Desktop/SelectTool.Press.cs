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
        if (hit == null && SeenOnly)
        {
            // Empty ground, or Alt held: a click deselects, a drag draws a rectangle on the screen.
            _box = (at, at, shift, false);
            return;
        }
        if (hit == null && ground is { } g2)
        {
            // Empty ground: a click deselects, a drag draws a zone to select.
            _lasso = (at, new List<Vector2> { new(g2.X, g2.Z) }, shift, false);
            return;
        }
        _view.Pick(at, size, shift, Range);
    }
}
