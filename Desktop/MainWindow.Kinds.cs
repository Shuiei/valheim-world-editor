using Avalonia.Controls;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The clipboard last remembered (or read back at start), so it is written only when it changes.
    /// </summary>
    private CopyData? _keptClip;

    /// <summary>
    /// Placing (or pasting) a kind switched off in View switches it on, so what was placed is seen; said after what the
    /// tool says about the placing, as the web editor did.
    /// </summary>
    /// <param name="scene">The scene the objects are in.</param>
    /// <param name="indices">The new objects' indices in it.</param>
    internal void ShowKindsOf(WorldScene scene, IReadOnlyList<int> indices)
    {
        var turned = new List<Said>();
        foreach (var k in indices.Where(i => i < scene.Things.Count).Select(i => ObjectKinds.Of(NameOfPrefab(scene.Things[i].Prefab), scene.Things[i].Piece, scene.Things[i].Tamed)).Distinct())
        {
            if (_kindBoxes.TryGetValue(k, out var box) && box.IsChecked != true)
            {
                box.IsChecked = true;
                turned.Add(ObjectKinds.Name(k));
            }
        }
        // (The Workshop has no View panel to say it in.)
        if (turned.Count == 0 || InWorkshop)
        {
            return;
        }
        Dispatcher.UIThread.Post(() =>
        {
            Msg(LastSaid + Said.Of("view.msg.switchedOn", Said.Join(", ", turned)));
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Eyedropper for the Replace lists: the next click on an object gives its kind (Esc cancels).
    /// </summary>
    /// <param name="label">What the kind is for, in the message.</param>
    /// <param name="done">Given the picked object's prefab.</param>
    internal void PickKind(string label, Action<int> done)
    {
        View.PickObjectOnce = i =>
        {
            if (i is int t && View.Scene is { } sc && t < sc.Things.Count)
            {
                done(sc.Things[t].Prefab);
            }
            else
            {
                Msg("view.msg.nothingPicked");
            }
        };
        // The label as the panel shows it; Claude reads its English.
        Msg(new Said(Lang.T("view.msg.pickKind", label), Lang.En("view.msg.pickKind", Lang.EnglishOf(label))));
    }

    /// <summary>
    /// , . turn the paste by 1° (Shift: 15°); . is clockwise seen from above, like the other tools.
    /// </summary>
    /// <returns>Always true: the key is taken.</returns>
    private bool TurnPaste(Avalonia.Input.Key key, bool shift)
    {
        float step = shift ? 15 : 1;
        View.Paste.TurnBy(key == Avalonia.Input.Key.OemComma ? step : -step);
        return true;
    }

    /// <summary>
    /// Alt + wheel: the tool that turns things turns them, as its , and . keys do; otherwise (false) it zooms.
    /// </summary>
    /// <returns>Whether a tool took it.</returns>
    internal bool AltWheel(Avalonia.Input.Key key, bool shift) => Tools.Mode switch
    {
        ToolMode.Paste => TurnPaste(key, shift),
        ToolMode.Place => PlaceInput.Key(key, shift, false),
        _ when Tools.SelectMode => View.SelectTool.Key(key, shift),
        _ => false,
    };

    // The View panel (top right): which kinds of objects are drawn, with how many the area has, and the water.
    /// <summary>
    /// One switch per kind of object.
    /// </summary>
    private readonly Dictionary<ObjectKind, CheckBox> _kindBoxes = [];

    /// <summary>
    /// The View panel's switch per kind of object.
    /// </summary>
    internal IReadOnlyDictionary<ObjectKind, CheckBox> KindBoxes => _kindBoxes;

    /// <summary>
    /// Draw the water.
    /// </summary>
    internal CheckBox WaterBox { get; } = new() { Content = Lang.T("view.label.water"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// Mark what is not saved yet (new objects keep a green marker).
    /// </summary>
    internal CheckBox UnsavedBox { get; } = new() { Content = Lang.T("view.label.unsaved"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// Mark ground points at the game's ±8 m limit in red.
    /// </summary>
    internal CheckBox LimitBox { get; } = new() { Content = Lang.T("view.label.limit"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// One switch per overlay layer.
    /// </summary>
    private readonly Dictionary<Overlays.Layer, CheckBox> _overlayBoxes = [];

    /// <summary>
    /// The View panel's switch per overlay layer.
    /// </summary>
    internal IReadOnlyDictionary<Overlays.Layer, CheckBox> OverlayBoxes => _overlayBoxes;

    /// <summary>
    /// Colour the ground by its slope.
    /// </summary>
    internal CheckBox SlopeBox { get; } = new() { Content = Lang.T("view.label.slope"), FontSize = 12 };
}
