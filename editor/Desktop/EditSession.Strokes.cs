using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The brush strokes: from the brush's first click to its release, with the mask judged when the stroke began.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// The mask over the ground as it is now (null when off).
    /// </summary>
    /// <returns>How much each grid point (by index) may change, 0 to 1; null when the mask is off.</returns>
    public Func<int, float>? MaskNow() => Mask.For(Ground, Scene.Biomes);

    /// <summary>
    /// The brush goes down at grid point (cx, cz). Flatten with TargetFromClick takes its target height from the ground
    /// there.
    /// </summary>
    public void BeginStroke(BrushTool tool, float cx, float cz)
    {
        Interlocked.Increment(ref _generation);
        lock (_lock)
        {
            _stroke = new Stroke { Tool = tool, Start = Ground.Snapshot() };
            _strokeDropped = false;
            // Judged as the ground was when the stroke started.
            _strokeMask = Mask.For(Ground, Scene.Biomes, frozen: true);
            // Flatten: level to the ground under the first click.
            if (tool == BrushTool.Flatten && Brush.TargetFromClick)
            {
                int gx = Math.Clamp((int)MathF.Round(cx), 0, Ground.W - 1), gz = Math.Clamp((int)MathF.Round(cz), 0, Ground.H - 1);
                _stroke.Target = Ground.HeightOf(gz * Ground.W + gx);
                Brush.Target = _stroke.Target.Value;
            }
        }
    }

    /// <summary>
    /// One frame of the stroke, the brush at (cx, cz), dt seconds after the last.
    /// </summary>
    /// <returns>False when no stroke is going on or the step changed nothing.</returns>
    public bool StrokeStep(float cx, float cz, float dt)
    {
        lock (_lock)
        {
            if (_stroke == null || Sculpt.Apply(Ground, Brush, _stroke, cx, cz, dt, _strokeMask) is not { } rect)
            {
                return false;
            }
            Touch(rect);
            return true;
        }
    }

    /// <summary>
    /// The brush comes up: the zones it touched go to the edit store and the stroke to the history.
    /// </summary>
    /// <returns>A message for the status line (empty when there is nothing to say): the game's ±8 m limit reached, or
    /// the Mask leaving out all the ground.</returns>
    public Said EndStroke()
    {
        Said message;
        lock (_lock)
        {
            var s = _stroke;
            _stroke = null;
            if (s == null)
            {
                if (_strokeDropped)
                {
                    _strokeDropped = false;
                    return Said.Of("brush.msg.dropped");
                }
                return Said.Empty;
            }
            if (s.Touched.Count > 0)
            {
                var zones = Ground.ZonesOf(s.Touched);
                Record(Brush.Label(s.Tool), s.Start, s.Touched, zones);
                Send(zones);
            }
            // After the stroke went into the edit store, as the Apply live that came back during it would have done.
            if (_takeEditsAfterStroke)
            {
                _takeEditsAfterStroke = false;
                Ground.TakeEdits(Edits);
            }
            message = s.Clamped ? Said.Of("brush.msg.limit")
                : s.Touched.Count == 0 && _strokeMask != null ? Said.Of("brush.msg.masked") : Said.Empty;
            _strokeMask = null;
        }
        Edited();
        return message;
    }
}
