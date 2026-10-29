using System.Numerics;

namespace TerrainEditor.Desktop;

// The preview of the placement under the cursor.
public sealed partial class PlaceTool
{
    /// <summary>The preview: what a click (Brush) or Enter (shapes) would place.</summary>
    /// <param name="at">The cursor's grid point (null off the ground).</param>
    /// <param name="under">The thing under the cursor (for On top).</param>
    public List<Placement> Preview(Vector2? at, int? under = null)
    {
        var s = Scene();
        var names = Chosen.ToList();
        Snapped = null;
        if (s == null || names.Count == 0 || Mode == Modes.Brush && at == null)
        {
            return [];
        }
        if (Building)
        {
            return BuildAt(s, names[0]) is { } one ? new() { one } : new();
        }
        var mask = Mask();
        var hash = Standing(s, MathF.Max(1, Spacing));
        var others = PiecesChosen && (SnapTo || Mode == Modes.Line) ? OtherSnaps(s, NameOf) : null;
        List<Placement> placed;
        if (Mode != Modes.Brush)
        {
            placed = ShapePlacements(s, names, hash, mask, others);
        }
        else
        {
            if (SettingsKey != _patternKey)
            {
                MakePattern();
            }
            // The layout turns with the rotation (clockwise from above, like the objects' facing).
            float t = RotationNow * MathF.PI / 180, c = MathF.Cos(t), sn = MathF.Sin(t);
            placed = [];
            foreach (var (d, dr) in _pattern)
            {
                var g = new Vector2(at!.Value.X + d.X * c + d.Y * sn, at.Value.Y - d.X * sn + d.Y * c);
                if (PlacementAt(s, g, dr, names, hash, mask) is { } o)
                {
                    placed.Add(o);
                }
            }
            if (placed.Count == 1 && SnapTo && others != null && IsPiece(placed[0].Name))
            {
                placed[0] = SnapSingle(s, placed[0], others, under);
            }
        }
        return RoomToGrow(placed);
    }
}
