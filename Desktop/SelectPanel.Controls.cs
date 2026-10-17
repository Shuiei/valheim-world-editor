using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The panel's controls, and the window's hooks into it.
public sealed partial class SelectPanel
{
    /// <summary>Explains the exact place boxes (where the selection is, or what Move by means).</summary>
    private readonly TextBlock _info = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>Deletes the selection.</summary>
    internal Button DeleteButton { get; }

    /// <summary>Adds every building piece connected to the selected ones to the selection.</summary>
    internal Button BuildingButton { get; }

    /// <summary>Selects every shown object of the selected kinds.</summary>
    internal Button SameButton { get; }

    /// <summary>Selects the shown objects that are not selected, and only those.</summary>
    internal Button InvertButton { get; }

    /// <summary>Puts each object on the ground when moving.</summary>
    internal CheckBox GroundBox { get; }

    /// <summary>Snaps moved pieces to other pieces.</summary>
    internal CheckBox SnapBox { get; }

    /// <summary>The exact place's X (east), in metres.</summary>
    internal NumericUpDown XBox { get; } = Num(0.1m);

    /// <summary>The exact place's Y (height), in metres.</summary>
    internal NumericUpDown YBox { get; } = Num(0.1m);

    /// <summary>The exact place's Z (north), in metres.</summary>
    internal NumericUpDown ZBox { get; } = Num(0.1m);

    /// <summary>The exact turn, in degrees clockwise seen from above.</summary>
    internal NumericUpDown TurnBox { get; } = Num(1m);

    /// <summary>Moves the selection there (or by the boxes' values with Move by).</summary>
    internal Button ApplyButton { get; } = new() { Content = Lang.T("select.label.moveThere"), FontSize = 12 };

    /// <summary>Opens the data of the one selected object (the window handles it).</summary>
    internal Button InspectButton { get; } = new() { Content = Lang.T("select.label.inspect"), FontSize = 12, IsEnabled = false };

    /// <summary>Make player built: gives the selected pieces the chosen builder (the window handles it).</summary>
    internal Button ClaimButton { get; } = new() { Content = Lang.T("select.label.claim"), FontSize = 12, IsEnabled = false };

    /// <summary>
    /// Replace with: the kinds this world can make (filled by the window, in <see cref="ReplaceKinds"/> order).
    /// </summary>
    internal ComboBox ReplaceBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };

    /// <summary>Replaces the selection with the kind chosen (raises <see cref="ReplaceAsked"/>).</summary>
    internal Button ReplaceButton { get; } = new() { Content = Lang.T("select.label.replace"), FontSize = 12 };

    /// <summary>Eyedropper: the "Replace with" kind picked from the world.</summary>
    internal Button ReplacePickButton { get; } = new() { Content = Lang.T("select.label.pick"), FontSize = 11, Padding = new Thickness(6, 2) };

    /// <summary>Switches Move by on or off.</summary>
    internal Button ByButton { get; } = new() { Content = Lang.T("select.label.moveBy"), FontSize = 12 };

    /// <summary>Saved selections (per world): the open world's, with their object counts.</summary>
    internal ComboBox SavedBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 300 };

    /// <summary>Keeps the selection under a name.</summary>
    internal Button KeepButton { get; } = new() { Content = Lang.T("select.label.keep"), FontSize = 12 };

    /// <summary>Selects the saved selection.</summary>
    internal Button LoadButton { get; } = new() { Content = Lang.T("rail.label.select"), FontSize = 12 };

    /// <summary>Adds the saved selection to what is selected.</summary>
    internal Button AddSavedButton { get; } = new() { Content = Lang.T("common.add"), FontSize = 12 };

    /// <summary>Forgets the saved selection.</summary>
    internal Button ForgetButton { get; } = new() { Content = Lang.T("select.label.forget"), FontSize = 12 };

    /// <summary>The prefab hashes of <see cref="ReplaceBox"/>'s entries, in its order.</summary>
    public List<int> ReplaceKinds { get; set; } = [];

    /// <summary>Replace the selection with this prefab.</summary>
    public event Action<int>? ReplaceAsked;

    /// <summary>
    /// Asks the window to take the next click on an object: the action's name for the prompt, and what to do with the
    /// picked prefab hash.
    /// </summary>
    public event Action<string, Action<int>>? PickKindAsked;

    /// <summary>A line for the status bar, in both languages.</summary>
    public event Action<Said>? Message;

    /// <summary>The name of a kind (the window's models), for messages.</summary>
    public Func<int, string?>? NameOf { get; set; }

    /// <summary>The window's: the open world's name (null while none).</summary>
    internal Func<string?> WorldName { get; set; } = () => null;

    /// <summary>The window's: the scene's things (null while none).</summary>
    internal Func<IReadOnlyList<WorldScene.Thing>?> Things { get; set; } = () => null;

    /// <summary>The window's: the selection, as indices into the things.</summary>
    internal Func<IReadOnlyCollection<int>> SelectedIds { get; set; } = Array.Empty<int>;

    /// <summary>The window's: selects these things.</summary>
    internal Action<IEnumerable<int>> SelectIds { get; set; } = _ => { };

    /// <summary>The window's: asks for a name, suggesting one; null or empty: cancelled.</summary>
    internal Func<string, Task<string?>> AskName { get; set; } = Task.FromResult<string?>;

    /// <summary>The window's: asks a yes/no question.</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
}
