using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Shape tool's panel, like the web editor's (editor/shapes.js): a click puts a shape into the
/// ground: a mound, cone, mesa, crater, moat or bowl, or any formula. Every preset is a formula too,
/// shown in the box, so it can be changed.
/// </summary>
public sealed partial class ShapePanel
{
    /// <summary>What a formula can use: metres east and north of the click, distance, radius, ground height.</summary>
    private static readonly string[] FormulaVars = ["x", "z", "d", "r", "h"];

    /// <summary>
    /// The preset shapes, each a formula; the list's last entry (index Presets.Length) is "Formula…", one of the
    /// user's own. Their shown names come from <see cref="NameOf"/>.
    /// </summary>
    public static readonly (string Key, string Formula)[] Presets =
    [
        ("mound", "h * smooth(1 - d / r)"),
        ("cone", "h * max(0, 1 - d / r)"),
        ("mesa", "h * smooth((r - d) / (0.35 * r))"),
        ("crater", "d < r ? -h * max(0, 1 - (d / (0.62 * r)) ^ 2) + 0.35 * h * bell((d - 0.72 * r) / (0.16 * r)) * smooth((r - d) / (0.1 * r)) : 0"),
        ("moat", "-h * bell((d - 0.75 * r) / (0.12 * r))"),
        ("bowl", "-h * smooth(1 - (d / r) ^ 2)"),
        ("ridges", "h * smooth(1 - d / r) * (0.75 + 0.25 * abs(sin(x / 3 + n(x / 9, z / 9) * 2)))"),
    ];

    /// <summary>A preset's name as the list shows it, in the language in use.</summary>
    /// <param name="key">The preset's key (<c>Presets</c>).</param>
    /// <exception cref="ArgumentOutOfRangeException">No preset has that key.</exception>
    public static string NameOf(string key) => key switch
    {
        "mound" => Lang.T("shape.preset.mound"),
        "cone" => Lang.T("shape.preset.cone"),
        "mesa" => Lang.T("shape.preset.mesa"),
        "crater" => Lang.T("shape.preset.crater"),
        "moat" => Lang.T("shape.preset.moat"),
        "bowl" => Lang.T("shape.preset.bowl"),
        "ridges" => Lang.T("shape.preset.ridges"),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "No such shape preset."),
    };

    /// <summary>The panel itself, shown beside the tool rail while the Shape tool is picked.</summary>
    public Control Card { get; }
    /// <summary>The shape's radius in metres (2..80): only points within it change.</summary>
    public float Radius { get; private set; } = 16;
    /// <summary>The shape's height in metres (the formula's h).</summary>
    public float Height { get; private set; } = 5;
    /// <summary>The compiled formula (null while it has a mistake): metres to add to the ground at a point.</summary>
    public Func<Formula.Env, double>? Function { get; private set; }
    /// <summary>The edit's name for the history, from the preset picked (or "Formula").</summary>
    public string Label => Lang.T("shape.undo", PresetBox.SelectedIndex < Presets.Length ? NameOf(Presets[PresetBox.SelectedIndex].Key) : Lang.T("shape.label.formula"));
    /// <summary>Raised when the radius, height or formula changes.</summary>
    public event Action? Changed;

    /// <summary>The preset list, with "Formula…" last.</summary>
    internal ComboBox PresetBox { get; }
    /// <summary>The radius slider, whole metres.</summary>
    internal Slider RadiusSlider { get; }
    /// <summary>The height box, in metres.</summary>
    internal NumericUpDown HeightBox { get; } = new() { Value = 5, Increment = 0.5m, FormatString = "0.0", FontSize = 12 };
    /// <summary>The formula's text; editing a preset's formula switches the list to "Formula…".</summary>
    internal TextBox FormulaBox { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("monospace"), FontSize = 12, MinHeight = 60 };
    /// <summary>The formula's mistake, empty while it compiles.</summary>
    internal TextBlock ErrorText { get; } = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(240, 150, 90)), TextWrapping = TextWrapping.Wrap };
    /// <summary>True while a preset writes its formula into the box, so that does not count as the user's change.</summary>
    private bool _settingPreset;

    /// <summary>Compiles the formula box's text into <c>Function</c>, shows any mistake and raises <c>Changed</c>.</summary>
    private void Sync()
    {
        try
        {
            Function = Formula.Compile(FormulaBox.Text ?? "", FormulaVars);
            ErrorText.Text = "";
        }
        catch (FormatException ex)
        {
            // A formula being typed: brief.
            Log.CaughtBrief(ex, "compiling the typed formula");
            Function = null;
            ErrorText.Text = Lang.T("shape.error.formula", ex.Message);
        }
        Changed?.Invoke();
    }
}
