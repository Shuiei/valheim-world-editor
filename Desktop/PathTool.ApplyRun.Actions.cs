
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
                    _clamped |= _g.SetHeight(s.P, s.H + (_tool.Amount + s.Bump) * s.W);
                    break;
                case Action.Lower:
                    _clamped |= _g.SetHeight(s.P, s.H - (_tool.Amount + s.Bump) * s.W);
                    break;
                case Action.Flatten:
                    _clamped |= _g.SetHeight(s.P, s.H + (_tool.Height + s.Bump - s.H) * s.W);
                    break;
                case Action.Ramp:
                {
                    float target = _tool.Start + (_tool.End - _tool.Start) * (s.Along / _total) + s.Bump;
                    _clamped |= _g.SetHeight(s.P, s.H + (target - s.H) * s.W);
                    break;
                }
                case Action.River:
                    DigRiver(s);
                    break;
                case Action.Cave:
                    DigCave(s);
                    break;
                case Action.Smooth:
                    Smooth(s);
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
                _clamped |= _g.SetHeight(s.P, s.H + (bed - s.H) * s.W);
            }
        }

        /// <summary>Digs the cave's floor under one point; it only digs.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void DigCave(in PointSample s)
        {
            // The floor across the cave's width; the soft edge makes its walls. It only digs, and keeps
            // within the game's ±8 m unless No limit is on (bumps above the ground's lie would reach it).
            float floor = s.CaveFloor + s.Bump * 0.3f;
            if (!_g.NoLimit)
            {
                floor = MathF.Max(floor, _g.Original(s.P) - Editing.EditStore.MaxLevel + 0.1f);
            }
            if (floor < s.H)
            {
                _clamped |= _g.SetHeight(s.P, s.H + (floor - s.H) * s.W);
            }
        }
    }
}
