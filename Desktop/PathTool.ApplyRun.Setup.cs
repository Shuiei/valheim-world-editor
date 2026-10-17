using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class PathTool
{
    private sealed partial class ApplyRun
    {
        /// <summary>
        /// Works out the cutoff, the rectangle looked at, the running length along the line, and the heights or paint
        /// the action needs before it changes anything.
        /// </summary>
        private void Prepare()
        {
            float edge = _soft > 0 ? _soft : 0.5f;
            // The farthest a changed point can be from the line: the widest a natural path gets, its soft edge (or the
            // half metre a hard edge shares), and the most its ragged edge reaches out; a metre more against rounding.
            // The box looked through is as wide, so the ragged edge is never cut straight.
            _cutoff = (_tool.Natural ? MathF.Abs(_half) * (1 + 0.45f * Noise.FbmBound) + edge + MathF.Max(_soft, 2) * 0.6f * Noise.FbmBound : _half + edge) + 1;
            _cum = new float[_curve.Count];
            for (int i = 1; i < _curve.Count; i++)
            {
                _cum[i] = _cum[i - 1] + Vector2.Distance(_curve[i].P, _curve[i - 1].P);
            }
            _x0 = Math.Max(1, (int)MathF.Floor(_curve.Min(c => c.P.X) - _cutoff));
            _x1 = Math.Min(_g.W - 2, (int)MathF.Ceiling(_curve.Max(c => c.P.X) + _cutoff));
            _z0 = Math.Max(1, (int)MathF.Floor(_curve.Min(c => c.P.Y) - _cutoff));
            _z1 = Math.Min(_g.H - 2, (int)MathF.Ceiling(_curve.Max(c => c.P.Y) + _cutoff));
            // Smoothing reads the heights from before.
            if (_tool.Act == Action.Smooth)
            {
                _before = new float[_g.W * _g.H];
                for (int i = 0; i < _before.Length; i++)
                {
                    _before[i] = _g.HeightOf(i);
                }
            }
            _colour = _tool.Act switch
            {
                Action.PaintDirt => Brush.PaintOf(BrushTool.PaintDirt),
                Action.PaintPaved => Brush.PaintOf(BrushTool.PaintPaved),
                Action.PaintCultivated => Brush.PaintOf(BrushTool.PaintCultivated),
                Action.PaintClear => Brush.PaintOf(BrushTool.PaintClear),
                _ => null,
            };
        }
    }
}
