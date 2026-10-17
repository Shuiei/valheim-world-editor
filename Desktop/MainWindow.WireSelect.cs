using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Wires the Select panel to the window and the 3D view: the selection and saved selections, picking a kind,
    /// Inspect (also asked from the view), Claim and Replace.
    /// </summary>
    private void WireSelectPanel()
    {
        SelectPanel.Message += Msg;
        SelectPanel.PickKindAsked += PickKind;
        SelectPanel.NameOf = NameOfPrefab;
        SelectPanel.WorldName = () => World?.World.Name is { Length: > 0 } n ? n : View.Scene?.World?.Name is { Length: > 0 } m ? m : null;
        SelectPanel.Things = () => View.Scene?.Things;
        SelectPanel.SelectedIds = () => View.Selected;
        SelectPanel.SelectIds = ids => { Tools.ChooseSelect(); View.Select(ids); };
        SelectPanel.AskName = initial => Dialogs.AskText(this, Lang.T("select.ask.keep.title"), Lang.T("select.ask.keep.text"), initial);
        SelectPanel.Confirm = text => Dialogs.Ask(this, Lang.T("select.ask.forget.title"), text, Lang.T("select.label.forget"));
        SelectPanel.InspectButton.Click += (_, _) => Inspect();
        View.InspectAsked += () => { if (View.Selected.Count == 1) { Inspect(); } };
        View.SelectToolAsked += Tools.ChooseSelect;
        SelectPanel.ClaimButton.Click += (_, _) => Claim();
        SelectPanel.ReplaceAsked += ReplaceSelection;
    }

    /// <summary>
    /// Replace (Select panel): the selected objects become the given kind.
    /// </summary>
    /// <param name="prefab">The kind they become.</param>
    private void ReplaceSelection(int prefab)
    {
        View.SelectTool.Commit();
        var sel = View.Selected.ToList();
        if (Session is not { } s || sel.Count == 0)
        {
            Msg("select.msg.replaceFirst");
            return;
        }
        AreaPanel!.Replace(s, sel, prefab);
        View.Select([]);
        UpdateSaveBar();
    }

    /// <summary>
    /// Wires the ground brushes: stamps, the tools' messages, No limit, loading a stamp picture and the brush's
    /// Alt + click.
    /// </summary>
    private void WireGroundTools()
    {
        View.StampClicked += StampAt;
        Tools.Message += Msg;
        Tools.NoLimitChanged += OnNoLimitChanged;
        Tools.LoadStampAsked += async () => await LoadStampAsync();
        View.BrushAltClick += OnBrushAltClick;
    }

    /// <summary>
    /// No limit switched (tool options): the session's ground and the Area panel's same switch follow.
    /// </summary>
    /// <param name="on">Whether the ground tools go past the game's ±8 m.</param>
    private void OnNoLimitChanged(bool on)
    {
        Session?.Ground.NoLimit = on;
        // The Area panel shows the same switch under Flatten, Raise and Lower.
        if (AreaPanel != null)
        {
            AreaPanel.NoLimitBox.IsChecked = on;
            AreaPanel.RefreshVolume();
        }
        Msg(on ? Said.Of("brush.msg.noLimitOn") : Said.Of("brush.msg.noLimitOff"));
    }
}
