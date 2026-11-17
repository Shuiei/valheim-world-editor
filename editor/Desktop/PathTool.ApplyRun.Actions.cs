
namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    internal sealed partial class ApplyRun
    {
        /// <summary>Applies the tool's action to one grid point, by the action.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void ActOn(in PointSample s)
        {
            switch (_tool.Act)
            {
                case Action.Raise:
                    _clamped |= GroundOps.Move(_g, s.P, (_tool.Amount + s.Bump) * s.W);
                    break;
                case Action.Lower:
                    _clamped |= GroundOps.Move(_g, s.P, -((_tool.Amount + s.Bump) * s.W));
                    break;
                case Action.Flatten:
                    _clamped |= GroundOps.Toward(_g, s.P, _tool.Height + s.Bump, s.W);
                    break;
                case Action.Ramp:
                    _clamped |= GroundOps.Toward(_g, s.P, _tool.Start + (_tool.End - _tool.Start) * (s.Along / _total) + s.Bump, s.W);
                    break;
                case Action.River:
                    DigRiver(s);
                    break;
                case Action.Smooth:
                    _clamped |= GroundOps.Smooth(_g, _before!, s.P, s.W);
                    break;
                case Action.Naturalize:
                    _clamped |= GroundOps.Naturalize(_g, _b, _before!, s.P, s.W);
                    break;
                case Action.Restore:
                    GroundOps.Restore(_g, s.P, s.W, s.W);
                    break;
                case Action.Erode:
                    // Erosion works on the whole reach of the line at once (Run does it, once every point is weighed).
                    _weighed.Add((s.P, s.W));
                    break;
                default:
                    Paint(s);
                    break;
            }
        }

        /// <summary>Digs the river bed under one point; it only digs.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void DigRiver(in PointSample s)
        {
            // A U-shaped bed: Depth below sea level in the middle, at the waterline at the path's
            // width, then the soft edge blends it into the banks. It only digs.
            float r = MathF.Min(s.DEff, s.HalfEff) / s.HalfEff;
            float bed = _water - _tool.Depth * MathF.Max(0, 1 - r * r) + s.Bump * 0.3f;
            if (bed < s.H)
            {
                _clamped |= GroundOps.Toward(_g, s.P, bed, s.W);
            }
        }
    }
}
