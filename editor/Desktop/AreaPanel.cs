using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Region tool's panel (the Area tool of the web editor's editor/area.js): the selection's shape and soft edge,
/// then what to do with it, in four groups (Ground, Objects, Recover, Clipboard), one action at a time with only its
/// own settings, and Apply (Enter). Ground actions change the ground inside; Objects chooses the objects inside (by
/// kind, and where the Mask lets the ground through) and hands them to the Select tool, which deletes or replaces
/// them; Recover brings back what was there (the generated ground, a backup, the game's nature, the zones as the
/// generator makes them); Clipboard copies and pastes.
/// </summary>
public sealed partial class AreaPanel
{
    /// <summary>The groups the actions are in: the panel's first choice.</summary>
    public enum Group
    {
        /// <summary>The ground inside: its height, its paint, a heightmap picture.</summary>
        Ground,
        /// <summary>The objects inside, handed to the Select tool.</summary>
        Objects,
        /// <summary>What was there, brought back.</summary>
        Recover,
        /// <summary>Copy, cut, paste and blueprints.</summary>
        Clipboard,
    }

    /// <summary>The actions, each in one group; each shows only its own rows.</summary>
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
        /// <summary>
        /// Selects the objects of the chosen kinds inside (or only one kind of object among them) and switches to the
        /// Select tool, where Delete removes them and Replace with turns them into another kind.
        /// </summary>
        Select,
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

    /// <summary>The groups, in the panel's order.</summary>
    internal static readonly Group[] Groups = [Group.Ground, Group.Objects, Group.Recover, Group.Clipboard];

    /// <summary>A group's actions, in the Action list's order. A group of one action shows no list.</summary>
    /// <param name="g">The group.</param>
    internal static Act[] ActsOf(Group g)
    {
        return g switch
        {
            Group.Ground => [Act.Flatten, Act.Raise, Act.Lower, Act.Smooth, Act.Natural, Act.Erode, Act.Paint, Act.Heightmap],
            Group.Objects => [Act.Select],
            Group.Recover => [Act.Restore, Act.Regrow, Act.Backup, Act.Reset],
            _ => [Act.Copy],
        };
    }

    /// <summary>Every action, group by group.</summary>
    internal static readonly Act[] Actions = [.. Groups.SelectMany(ActsOf)];

    /// <summary>The group an action is in.</summary>
    /// <param name="a">The action.</param>
    internal static Group GroupOf(Act a) => Groups.First(g => ActsOf(g).Contains(a));

    /// <summary>A group's name on its button.</summary>
    /// <param name="g">The group.</param>
    private static string GroupLabel(Group g)
    {
        return g switch
        {
            Group.Ground => Lang.T("area.group.ground"),
            Group.Objects => Lang.T("area.group.objects"),
            Group.Recover => Lang.T("area.group.recover"),
            _ => Lang.T("area.group.clipboard"),
        };
    }

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
            Act.Select => Lang.T("area.action.select"),
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

    /// <summary>The panel itself, shown over the view while the Region tool is on.</summary>
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

    /// <summary>
    /// The object names behind the Only list's entries after its first ("every chosen kind"), in its order (the
    /// entries add a count to them).
    /// </summary>
    private List<string> _onlyNames = [];

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

    /// <summary>The group shown.</summary>
    public Group CurrentGroup { get; private set; } = Group.Ground;

    /// <summary>The action each group was last on: the one it shows again when its button is pressed.</summary>
    private readonly Dictionary<Group, Act> _last = Groups.ToDictionary(g => g, g => ActsOf(g)[0]);

    /// <summary>The Action list is being filled for another group: its selection is not a choice yet.</summary>
    private bool _filling;

    /// <summary>The action chosen: the one on the shown group's list.</summary>
    public Act Current
    {
        get
        {
            var acts = ActsOf(CurrentGroup);
            return acts[Math.Clamp(ActionBox.SelectedIndex, 0, acts.Length - 1)];
        }
    }

    /// <summary>An action was chosen (a group's button, the Action list, <see cref="Choose"/>).</summary>
    public event Action? ActionChosen;

    /// <summary>Chooses an action: shows its group, with the action on its list.</summary>
    internal void Choose(Act a) => ShowGroup(GroupOf(a), a);

    /// <summary>Shows a group: its button on, its actions on the Action list (hidden for a group of one).</summary>
    /// <param name="g">The group.</param>
    /// <param name="act">The action to show; null: the one the group was last on.</param>
    internal void ShowGroup(Group g, Act? act = null)
    {
        CurrentGroup = g;
        foreach (var (group, button) in GroupButtons)
        {
            button.Classes.Set("on", group == g);
        }
        var acts = ActsOf(g);
        _filling = true;
        ActionBox.ItemsSource = acts.Select(ActionLabel).ToArray();
        ActionBox.SelectedIndex = Math.Max(0, Array.IndexOf(acts, act ?? _last[g]));
        _filling = false;
        _actionRow.IsVisible = acts.Length > 1;
        ActionChanged();
    }

    /// <summary>The action shown changed: its rows, the backups when it restores from one, and those who listen.</summary>
    private void ActionChanged()
    {
        _last[CurrentGroup] = Current;
        // ShowRows refreshes the panel, the volume with it.
        ShowRows();
        if (Current == Act.Backup)
        {
            FillBackups();
        }
        ActionChosen?.Invoke();
    }
}
