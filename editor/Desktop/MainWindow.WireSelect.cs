using TerrainEditor.App;
using TerrainEditor.Save;

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
    /// The world and its templates, the game's data, the manifests and the mods' catalogue the Select panel's Replace
    /// with list was worked out from; null: not worked out for the session edited yet.
    /// </summary>
    private (WorldSave? World, int Templates, int Data, int Manifests, int Catalogue)? _replaceKindsFrom;

    /// <summary>
    /// The kinds the Select tool can replace with (Replace with): what the edited world can make, by name. Worked out
    /// again for another world (its templates are its own), when a live merge brings it new kinds, or when the game's
    /// data, the mods' manifests or their catalogue change, keeping the chosen kind when it is still offered; left as
    /// it is otherwise.
    /// </summary>
    internal void FillReplaceKinds()
    {
        var w = Session?.Scene.World;
        (WorldSave? World, int Templates, int Data, int Manifests, int Catalogue) from =
            (w, w?.TemplatesGeneration ?? 0, GameData.Generation, ModManifests.Generation, ModCatalogue.Generation);
        if (_replaceKindsFrom == from)
        {
            return;
        }
        _replaceKindsFrom = from;
        int at = SelectPanel.ReplaceBox.SelectedIndex;
        int chosen = at >= 0 && at < SelectPanel.ReplaceKinds.Count ? SelectPanel.ReplaceKinds[at] : 0;
        var kinds = w?.Creatable.Where(p => NameOfPrefab(p) != null).OrderBy(NameOfPrefab, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        SelectPanel.ReplaceKinds = kinds;
        SelectPanel.ReplaceBox.ItemsSource = kinds.Select(p => NameOfPrefab(p)!).ToList();
        SelectPanel.ReplaceBox.SelectedIndex = kinds.IndexOf(chosen);
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
        Tools.Message += Msg;
        MaskPanel.NoLimitChanged += OnNoLimitChanged;
        Tools.LoadStampAsked += async () => await LoadStampAsync();
        View.BrushAltClick += OnBrushAltClick;
    }

    /// <summary>
    /// No limit switched (the Mask card): the session's ground follows, and the Area panel's cut and fill, which
    /// counts what the limit leaves out of reach.
    /// </summary>
    /// <param name="on">Whether the ground tools go past the game's ±8 m.</param>
    private void OnNoLimitChanged(bool on)
    {
        Session?.Ground.NoLimit = on;
        AreaPanel?.RefreshVolume();
        Msg(on ? Said.Of("mask.msg.noLimitOn") : Said.Of("mask.msg.noLimitOff"));
    }
}
