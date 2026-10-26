using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Fitting the settings to what the chosen kinds can do: the widest kind, the spacing it allows, and the hint for it.
public sealed partial class PlaceTool
{
    /// <summary>
    /// The chosen kind that is widest at its largest size (m, across its model's box), for the fit hint; (0, null)
    /// while no chosen kind's model is known.
    /// </summary>
    public (float Width, string? Who) Widest()
    {
        float w = 0;
        string? who = null;
        foreach (string n in Chosen)
        {
            if (ModelBox(n) is var (lo, hi) && MathF.Max(hi.X - lo.X, hi.Z - lo.Z) is float x && x > w)
            {
                (w, who) = (x, n);
            }
        }
        return (w * MathF.Max(SizeMin, SizeMax) / 100, who);
    }

    /// <summary>
    /// The spacing the mode goes by: centre to centre in Grid and Line, the least distance in Brush and Zone; none for
    /// End to end lines (the pieces' own length sets it).
    /// </summary>
    public float? SpacingNow => Mode switch
    {
        Modes.Grid => Cell,
        Modes.Line => EndToEnd ? null : Every,
        _ => Spacing,
    };

    /// <summary>
    /// Says when the widest chosen kind is wider than the spacing (so they overlap), as the web editor did; null when
    /// they fit.
    /// </summary>
    public string? FitHint()
    {
        if (SpacingNow is not float v || Widest() is not (var w, string who) || w <= v + 0.25f)
        {
            return null;
        }
        return Lang.T("place.hint.fit", who, w, v);
    }

    /// <summary>
    /// Fit: the spacing set to the widest chosen kind (to the next half metre, within the setting's range), so they
    /// stand side by side without overlapping. False when there is nothing to fit.
    /// </summary>
    public bool Fit()
    {
        var (w, who) = Widest();
        if (SpacingNow == null || who == null || w <= 0)
        {
            return false;
        }
        float Up(float max) => MathF.Min(max, MathF.Ceiling(w * 2) / 2);
        switch (Mode)
        {
            case Modes.Grid:
                Cell = Up(MaxCell);
                break;
            case Modes.Line:
                Every = Up(MaxEvery);
                break;
            default:
                Spacing = Up(MaxSpacing);
                break;
        }
        NewLayout();
        return true;
    }
}
