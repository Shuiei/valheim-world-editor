using Avalonia.Input;

namespace TerrainEditor.Desktop;

public partial class MainWindow
{
    /// <summary>
    /// The 3D editor's keys, in the order they are tried: the first rule that matches wins.
    /// </summary>
    private KeyRule[] KeyRules()
    {
        bool InMode(ToolMode mode) => Tools.Mode == mode;
        bool Pasting(KeyPress _) => InMode(ToolMode.Paste);
        return
        [
            Take([Key.Escape], KeyMods.NoCtrl, _ => View.PickObjectOnce != null, _ =>
            {
                View.PickObjectOnce = null;
                Msg("view.msg.pickCancelled");
            }),
            // The right-hand panels, as in the web editor: V the View panel, L the history, ? (or F3) the help. ? is Shift
            // and the / key (OemQuestion and Oem2 are that one key): / alone is not it.
            Take([Key.F3], KeyMods.NoCtrl, null, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.OemQuestion], KeyMods.NoCtrl, p => p.Shift, _ => ShowRight(HelpCard.IsVisible ? null : HelpCard)),
            Take([Key.V], KeyMods.None, null, _ => ShowRight(ViewPanelCard.IsVisible ? null : ViewPanelCard)),
            Take([Key.L], KeyMods.None, null, _ => ShowRight(History.Card.IsVisible ? null : History.Card)),
            Take([Key.Z], KeyMods.Ctrl, null, p => _ = p.Shift ? Redo() : Undo()),
            Take([Key.Y], KeyMods.Ctrl, null, p => _ = Redo()),
            Take([Key.C], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Area or ToolMode.Select, _ => Copy()),
            Take([Key.X], KeyMods.Ctrl, _ => Tools.Mode is ToolMode.Area or ToolMode.Select, _ => Cut()),
            Take([Key.V], KeyMods.Ctrl, null, _ => StartPaste()),
            // Pasting: R turns a quarter, F mirrors, , and . turn by 1° (Shift: 15°), Esc ends it.
            Take([Key.R], KeyMods.NoCtrl, Pasting, _ => View.Paste.TurnBy(90)),
            Take([Key.F], KeyMods.NoCtrl, Pasting, _ =>
            {
                View.Paste.Mirror = !View.Paste.Mirror;
                View.Paste.Notify();
            }),
            Take([Key.OemComma, Key.OemPeriod], KeyMods.NoCtrl, Pasting, p => TurnPaste(p.Key, p.Shift)),
            Take([Key.Escape], KeyMods.NoCtrl, Pasting, _ => Tools.ChooseMode(ToolMode.Area)),
            Take([Key.S], KeyMods.Ctrl, null, p => _ = Save()),
            Take([Key.OemOpenBrackets, Key.OemCloseBrackets], KeyMods.NoCtrl, null, p => Tools.ResizeBrush(p.Key == Key.OemOpenBrackets ? -1 : 1)),
            Take([Key.Escape], KeyMods.NoCtrl, _ => Inspector.IsOpen, _ => Inspector.Close()),
            Take([Key.I], KeyMods.NoCtrl, _ => Tools.SelectMode && View.Selected.Count == 1, _ => Inspect()),
            // Alt + Z: Select through on or off, as Blender's X-ray.
            Take([Key.Z], KeyMods.NoCtrl, p => Tools.SelectMode && p.Mods.HasFlag(KeyModifiers.Alt), _ => SelectPanel.ThroughBox.IsChecked = SelectPanel.ThroughBox.IsChecked != true),
            // The Place and Select tools' own keys (any modifiers): what they turn down goes on.
            Offer(null, KeyMods.Any, _ => InMode(ToolMode.Place), p => PlaceInput.Key(p.Key, p.Shift, p.Ctrl)),
            Offer(null, KeyMods.Any, _ => Tools.SelectMode, p => View.SelectTool.Key(p.Key, p.Shift)),
            Take([Key.Enter, Key.Return], KeyMods.NoCtrl, _ => InMode(ToolMode.Area), p =>
            {
                if (!View.Area.CloseWithEnter())
                {
                    _ = AreaPanel.Apply();
                }
            }),
            Take([Key.Back], KeyMods.NoCtrl, _ => InMode(ToolMode.Area), _ => View.Area.RemoveLast()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Area) && View.Area.Points.Count > 0, _ => View.Area.Clear()),
            Take([Key.Enter, Key.Return], KeyMods.NoCtrl, _ => InMode(ToolMode.Path), _ => ApplyPath()),
            Take([Key.Back], KeyMods.NoCtrl, _ => InMode(ToolMode.Path), _ => View.Path.RemoveLast()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Path) && View.Path.Points.Count > 0, _ => View.Path.Clear()),
            Offer([Key.Delete], KeyMods.NoCtrl, _ => InMode(ToolMode.Dungeon), _ => View.Dungeon.Delete()),
            Take([Key.R], KeyMods.NoCtrl, _ => InMode(ToolMode.Dungeon), _ => View.Dungeon.Turn()),
            Take([Key.Escape], KeyMods.NoCtrl, _ => InMode(ToolMode.Measure) && View.Tape.A != null, _ => View.Tape.Clear()),
            // The rail's keys: the tool panel acts on them, and they stay unhandled.
            Pass([Key.Escape], KeyMods.NoCtrl, null, _ => Tools.Key("Escape")),
            Pass([Key.E, Key.M, Key.G, Key.P, Key.B, Key.O, Key.T, Key.H], KeyMods.NoCtrl, null, p => Tools.Key(p.Key.ToString())),
            Pass(Digits(Key.D0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.D0).ToString())),
            Pass(Digits(Key.NumPad0), KeyMods.NoCtrl, null, p => Tools.Key((p.Key - Key.NumPad0).ToString())),
        ];
    }
}
