using Avalonia.Controls;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Area tool's panel, like the web editor's (editor/area.js): the selection's shape and soft edge,
/// one action at a time with only its own settings, and Apply (Enter). Ground actions change the ground
/// inside; object actions remove, select or replace the objects of the chosen kinds inside; Reset zones
/// hands the zones under the selection back to the world generator when saving.
/// </summary>
public sealed partial class AreaPanel
{
    /// <summary>The actions on the Action list, in its order; each shows only its own rows.</summary>
    public enum Act
    {
        /// <summary>Flattens the ground inside to one height.</summary>
        Flatten,
        /// <summary>Raises the ground inside.</summary>
        Raise,
        /// <summary>Lowers the ground inside.</summary>
        Lower,
        /// <summary>Evens out bumps inside.</summary>
        Smooth,
        /// <summary>Natural-looking bumps (Naturalize), with the Naturalize brush's Bumps and Size.</summary>
        Natural,
        /// <summary>Back to the ground the game generated, paint removed.</summary>
        Restore,
        /// <summary>Weathers the ground inside: slopes settle and rain cuts gullies.</summary>
        Erode,
        /// <summary>Paints the ground inside.</summary>
        Paint,
        /// <summary>Removes the objects of the chosen kinds inside.</summary>
        Remove,
        /// <summary>Selects the objects of the chosen kinds inside.</summary>
        Select,
        /// <summary>
        /// Replaces every object inside of the prefab chosen in Replace with the one chosen in With, whatever
        /// the kind toggles say.
        /// </summary>
        Replace,
        /// <summary>Puts back what the game grows here, by its vegetation rules, for the chosen kinds.</summary>
        Regrow,
        /// <summary>Copies the selection, to paste it elsewhere.</summary>
        Copy,
        /// <summary>Exports the ground inside as a heightmap picture, or imports one.</summary>
        Heightmap,
        /// <summary>Restores the selection from a backup.</summary>
        Backup,
        /// <summary>Hands the zones under the selection back to the world generator when saving.</summary>
        Reset,
    }

    /// <summary>The Action list's actions, in its order.</summary>
    private static readonly Act[] Actions =
    [
        Act.Flatten, Act.Raise, Act.Lower, Act.Smooth, Act.Natural, Act.Restore, Act.Erode, Act.Paint,
        Act.Remove, Act.Select, Act.Replace, Act.Regrow, Act.Copy, Act.Heightmap, Act.Backup, Act.Reset,
    ];

    /// <summary>An action's name on the Action list.</summary>
    /// <param name="a">The action.</param>
    private static string ActionLabel(Act a)
    {
        return a switch
        {
            Act.Flatten => Lang.T("brush.label.flatten"),
            Act.Raise => Lang.T("brush.label.raise"),
            Act.Lower => Lang.T("brush.label.lower"),
            Act.Smooth => Lang.T("brush.label.smooth"),
            Act.Natural => Lang.T("brush.label.natural"),
            Act.Restore => Lang.T("area.action.restore"),
            Act.Erode => Lang.T("brush.label.erode"),
            Act.Paint => Lang.T("common.paint"),
            Act.Remove => Lang.T("area.action.remove"),
            Act.Select => Lang.T("area.action.select"),
            Act.Replace => Lang.T("area.action.replace"),
            Act.Regrow => Lang.T("area.action.regrow"),
            Act.Copy => Lang.T("area.action.copy"),
            Act.Heightmap => Lang.T("area.action.heightmap"),
            Act.Backup => Lang.T("area.action.backup"),
            _ => Lang.T("area.action.reset"),
        };
    }

    /// <summary>The Paint action's paints: the brush tools whose paint they put down, in the list's order.</summary>
    private static readonly BrushTool[] Paints = [BrushTool.PaintDirt, BrushTool.PaintCultivated, BrushTool.PaintPaved, BrushTool.PaintClear];

    /// <summary>A paint's name on the Paint list.</summary>
    /// <param name="t">The paint's brush tool.</param>
    private static string PaintLabel(BrushTool t)
    {
        return t switch
        {
            BrushTool.PaintDirt => Lang.T("brush.label.dirt"),
            BrushTool.PaintCultivated => Lang.T("area.paint.cultivated"),
            BrushTool.PaintPaved => Lang.T("brush.label.paved"),
            _ => Lang.T("brush.label.clear"),
        };
    }

    /// <summary>Painting with a paint, as the action's name in messages and History, in both languages.</summary>
    /// <param name="t">The paint's brush tool.</param>
    private static Said PaintNamed(BrushTool t)
    {
        return t switch
        {
            BrushTool.PaintDirt => Said.Of("path.action.paintDirt"),
            BrushTool.PaintCultivated => Said.Of("path.action.paintCultivated"),
            BrushTool.PaintPaved => Said.Of("path.action.paintPaved"),
            _ => Said.Of("area.paintAct.clear"),
        };
    }

    /// <summary>The panel itself, shown over the view while the Area tool is on.</summary>
    public Control Card { get; }

    /// <summary>The view's selection (box or polygon) the actions work on.</summary>
    public AreaTool Area { get; }

    private readonly GlView _view;

    private readonly Func<EditSession?> _session;

    private readonly Func<int, string?> _nameOf;

    /// <summary>
    /// Whether an Apply is running: it can wait on a question, the generator or a backup being read, and a second one
    /// meanwhile would do the same again.
    /// </summary>
    private bool _applying;

    /// <summary>The kinds' names in the Replace list, in its order (its entries add a count to them).</summary>
    private List<string> _fromNames = [];

    /// <summary>Asks a yes/no question (the window's dialog); resetting zones goes on only on true.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);

    /// <summary>A line for the status bar, in the user's language and in English (for Claude).</summary>
    public event Action<Said>? Message;

    /// <summary>Raises <see cref="Message"/>.</summary>
    private void Say(Said s) => Message?.Invoke(s);

    /// <summary>Select objects: the window switches to the Select tool.</summary>
    public event Action? SwitchToSelect;

    /// <summary>The rows shown only for some actions, with those actions.</summary>
    private readonly Dictionary<Control, Act[]> _rows = [];

    /// <summary>The prefab hashes behind <see cref="ToBox"/>'s entries, in its order.</summary>
    private List<int> _creatable = [];

    /// <summary>
    /// The world and its templates, the game's data, the manifests and the mods' catalogue <c>_creatable</c> was
    /// worked out from.
    /// </summary>
    private (WorldSave? World, int Templates, int Data, int Manifests, int Catalogue) _creatableFrom;

    /// <summary>The action chosen on the list.</summary>
    public Act Current => Actions[Math.Max(0, ActionBox.SelectedIndex)];

    /// <summary>Chooses an action on the list.</summary>
    internal void Choose(Act a) => ActionBox.SelectedIndex = Array.IndexOf(Actions, a);
}
