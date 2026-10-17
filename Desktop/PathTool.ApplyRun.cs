using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    /// <summary>
    /// One Apply: visits the grid points around the line once, and changes each one that the line's reach and soft edge
    /// let through, by the action.
    /// </summary>
    private sealed partial class ApplyRun
    {
        /// <summary>The tool whose action and values this run applies.</summary>
        private readonly PathTool _tool;
        /// <summary>The ground to change.</summary>
        private readonly Ground _g;
        /// <summary>The brush, for its noise (Natural look).</summary>
        private readonly Brush _b;
        /// <summary>The water level, for River.</summary>
        private readonly float _water;
        /// <summary>Per grid point weight from 0 to 1 (0 leaves it alone); null changes every point.</summary>
        private readonly Func<int, float>? _mask;
        /// <summary>The cave worked out before the ground changes (Cave); null when none was given.</summary>
        private readonly CavePlan? _cave;
        /// <summary>The dense line the action follows.</summary>
        private readonly List<(Vector2 P, int Seg)> _curve;
        /// <summary>The grid points changed, by index.</summary>
        private readonly List<int> _touched = [];
        /// <summary>Whether the game's height limit stopped some change.</summary>
        private bool _clamped;
        /// <summary>Half the path's width, in metres.</summary>
        private readonly float _half;
        /// <summary>The soft edge, in metres.</summary>
        private readonly float _soft;
        /// <summary>The line's length, in metres (at least a millionth).</summary>
        private readonly float _total;
        /// <summary>The origin of the scene's grid, X.</summary>
        private readonly float _ox;
        /// <summary>The origin of the scene's grid, Z.</summary>
        private readonly float _oz;
        /// <summary>How far from the line a point can be and still be looked at, in metres.</summary>
        private float _cutoff;
        /// <summary>The first and last grid columns and rows looked at.</summary>
        private int _x0, _z0, _x1, _z1;
        /// <summary>How far along the line each point of the dense line is, in metres.</summary>
        private float[] _cum = [];
        /// <summary>The heights before Smooth reads them; null for the other actions.</summary>
        private float[]? _before;
        /// <summary>The paint colour (RGBA) of a paint action; null for the others.</summary>
        private float[]? _colour;

        /// <summary>Starts a run over the given line.</summary>
        /// <param name="tool">The tool whose action and values apply.</param>
        /// <param name="g">The ground to change.</param>
        /// <param name="b">The brush, for its noise.</param>
        /// <param name="water">The water level, for River.</param>
        /// <param name="mask">Per grid point weight; null changes every point.</param>
        /// <param name="cave">The cave worked out before the ground changes, or null.</param>
        /// <param name="curve">The dense line.</param>
        public ApplyRun(PathTool tool, Ground g, Brush b, float water, Func<int, float>? mask, CavePlan? cave, List<(Vector2 P, int Seg)> curve)
        {
            _tool = tool;
            _g = g;
            _b = b;
            _water = water;
            _mask = mask;
            _cave = cave;
            _curve = curve;
            _half = tool.Width / 2;
            _soft = tool.Soft;
            _total = Math.Max(Length(curve), 1e-6f);
            _ox = g.OriginX;
            _oz = g.OriginZ;
        }

        /// <summary>
        /// Changes every grid point around the line that the action reaches.
        /// </summary>
        /// <returns>The points changed, the rectangle around them (one point wider each way), and whether the game's limit stopped some.</returns>
        internal (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Run()
        {
            Prepare();
            var near = NearestOnLine(_curve, _x0, _z0, _x1, _z1, _cutoff);
            int nw = _x1 - _x0 + 1;
            for (int gz = _z0; gz <= _z1; gz++)
            {
                for (int gx = _x0; gx <= _x1; gx++)
                {
                    if (_g.Locked(gx, gz))
                    {
                        continue;
                    }
                    Point(gx, gz, near, nw);
                }
            }
            return (_touched, (_x0 - 1, _z0 - 1, _x1 + 1, _z1 + 1), _clamped);
        }

        /// <summary>
        /// One grid point: where the line is nearest to it, how far along and how far from it, and how much it is changed.
        /// </summary>
        /// <param name="gx">The grid column.</param>
        /// <param name="gz">The grid row.</param>
        /// <param name="near">The nearest points of the line to the rectangle.</param>
        /// <param name="nw">The rectangle's width in points.</param>
        private void Point(int gx, int gz, Nearest near, int nw)
        {
            // The nearest point of the line: distance, and how far along it. None within the cutoff: too far to change.
            int ni = (gz - _z0) * nw + (gx - _x0);
            int seg = near.Seg[ni];
            if (seg == 0)
            {
                return;
            }
            float best = near.Dist[ni];
            float st = near.T[ni];
            float along = _cum[seg - 1] + st * MathF.Sqrt(Segment(_curve, seg).L2), caveFloor = 0;
            if (_cave != null)
            {
                caveFloor = _cave.Floor[seg - 1] + (_cave.Floor[seg] - _cave.Floor[seg - 1]) * st;
            }
            float dEff = best, halfEff = _half, bump = 0;
            if (_tool.Natural)
            {
                // Width varies along the path, edges are ragged, and the surface gets bumps.
                halfEff = _half * (1 + 0.45f * _b.Noise.Fbm(along / (_b.NoiseSize * 1.5f) + 31.7f, 4.2f));
                dEff = best + MathF.Max(_soft, 2) * 0.6f * WorldNoise(gx, gz, 0.6f);
                bump = _b.NoiseAmp * WorldNoise(gx + 500, gz - 300);
            }
            // Without a soft edge, the points the edge crosses (within half a metre) get a share, so the
            // edge follows the line straight instead of stepping from point to point.
            if (dEff > halfEff + (_soft > 0 ? _soft : 0.5f))
            {
                return;
            }
            float f = _soft > 0 ? (dEff <= halfEff ? 1 : 1 - (dEff - halfEff) / _soft) : Math.Clamp(halfEff + 0.5f - dEff, 0, 1);
            int p = gz * _g.W + gx;
            float w = (_soft > 0 ? f * f * (3 - 2 * f) : f) * (_mask?.Invoke(p) ?? 1);
            if (w <= 0)
            {
                return;
            }
            float h = _g.HeightOf(p);
            _touched.Add(p);
            ActOn(new PointSample(gx, gz, p, h, w, along, bump, caveFloor, dEff, halfEff));
        }

        /// <summary>A brush's noise at a view-space point of the grid, scaled.</summary>
        /// <param name="gx">The grid column.</param>
        /// <param name="gz">The grid row.</param>
        /// <param name="scale">How much larger the noise's features are than the brush's.</param>
        /// <returns>The noise value.</returns>
        private float WorldNoise(float gx, float gz, float scale = 1) => _b.Noise.Fbm((_ox + gx) / (_b.NoiseSize * scale), (_oz + gz) / (_b.NoiseSize * scale));

        /// <summary>What is known about one grid point the line reaches.</summary>
        /// <param name="Gx">The grid column.</param>
        /// <param name="Gz">The grid row.</param>
        /// <param name="P">The point's index in the grid.</param>
        /// <param name="H">The ground's height there, in metres.</param>
        /// <param name="W">The weight of the action there, from 0 to 1.</param>
        /// <param name="Along">How far along the line the point is, in metres.</param>
        /// <param name="Bump">The Natural look's bump, in metres.</param>
        /// <param name="CaveFloor">The cave's floor there, in metres.</param>
        /// <param name="DEff">The distance from the line, with the Natural look's ragged edge.</param>
        /// <param name="HalfEff">The half width there, with the Natural look's variation.</param>
        private readonly record struct PointSample(int Gx, int Gz, int P, float H, float W, float Along, float Bump, float CaveFloor, float DEff, float HalfEff);
    }
}
