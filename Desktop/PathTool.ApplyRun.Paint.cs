
namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    internal sealed partial class ApplyRun
    {
        /// <summary>Evens out one point: its height moves to the average of the heights around it, from before.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void Smooth(in PointSample s)
        {
            float sum = 0;
            int n = 0;
            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    // Inside the area on both axes (a flat index would wrap into the next row).
                    int x = s.Gx + dx, z = s.Gz + dz;
                    if (x >= 0 && x < _g.W && z >= 0 && z < _g.H)
                    {
                        sum += _before![z * _g.W + x];
                        n++;
                    }
                }
            }
            _clamped |= _g.SetHeight(s.P, s.H + (sum / n - s.H) * s.W);
        }

        /// <summary>Paints one point with the paint action's colour, by its weight.</summary>
        /// <param name="s">The point and what is known about it.</param>
        internal void Paint(in PointSample s)
        {
            _g.StartPaint(s.P);
            for (int c = 0; c < 4; c++)
            {
                _g.Paint[s.P * 4 + c] += (_colour![c] - _g.Paint[s.P * 4 + c]) * s.W;
            }
        }
    }
}
