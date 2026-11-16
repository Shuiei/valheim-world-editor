using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// What is remembered between runs (Prefs, as the web editor kept it in the browser): each control set from it now
    /// and remembered as it changes.
    /// </summary>
    private void RememberPrefs()
    {
        var p = Prefs;
        // View: the kinds drawn, water, overlays, the look's slope colours and height lines.
        foreach (var (k, box) in _kindBoxes)
        {
            p.Bind(box, $"view.show.{k}");
        }
        p.Bind(WaterBox, "view.water");
        p.Bind(UnsavedBox, "view.unsaved");
        p.Bind(LimitBox, "view.limit");
        foreach (var (layer, box) in _overlayBoxes)
        {
            p.Bind(box, $"view.overlay.{layer}");
        }
        p.Bind(SlopeBox, "view.slope");
        p.Bind(ContourBox, "view.contour");
        p.Bind(ContourStepBox, "view.contourStep");
        // The look: the game's or plain, buildings see-through, the 3D view's resolution.
        p.Bind(GameLookBox, "view.gameLook");
        p.Bind(SeeThroughBox, "view.seeThrough");
        p.Bind(ResolutionBox, "view.res3d");
        // The brush's shape and falloff, the Region action, Select's options.
        p.Bind(Tools.ShapeBox, "brush.shape");
        p.Bind(Tools.FalloffBox, "brush.falloff");
        // The Region action by its own name, whatever its group and the language: one no longer there (a name kept by
        // an older version) leaves the panel as it is.
        if (p.TryGet("area.action", out string? act) && Enum.TryParse<AreaPanel.Act>(act, out var kept) && Enum.IsDefined(kept))
        {
            AreaPanel.Choose(kept);
        }
        AreaPanel.ActionChosen += () => p.Set("area.action", AreaPanel.Current.ToString());
        p.Bind(SelectPanel.GroundBox, "select.onGround");
        p.Bind(SelectPanel.ZoneBox, "select.zone");
        p.Bind(SelectPanel.ThroughBox, "select.through");
        p.Bind(SelectPanel.PickBox, "select.picks");
        // Landform: the kind, and the Formula kind's preset, with the formula when it is one's own.
        p.Bind(LandformPanel.KindBox, "landform.kind");
        var shape = LandformPanel.Formula;
        p.Bind(shape.PresetBox, "shape.preset");
        bool Own() => shape.PresetBox.SelectedIndex == ShapePanel.Presets.Length;
        if (Own() && p.TryGet("shape.formula", out string? formula) && formula != null)
        {
            shape.FormulaBox.Text = formula;
        }
        shape.FormulaBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && Own())
            {
                p.Set("shape.formula", shape.FormulaBox.Text ?? "");
            }
        };
        // The Place tool's list: which categories are open.
        if (p.TryGet("place.openKinds", out string[]? open) && open != null)
        {
            PlacePanel.OpenKinds.Clear();
            foreach (string name in open)
            {
                if (Enum.TryParse<ObjectKind>(name, out var k))
                {
                    PlacePanel.OpenKinds.Add(k);
                }
            }
        }
        PlacePanel.OpenKindsChanged += () => p.Set("place.openKinds", PlacePanel.OpenKinds.Select(k => k.ToString()).Order().ToArray());
        // The right-hand panel open.
        ShowRight(ChosenRight());
        // The clipboard (its objects' ids only meant something in the world it came from).
        if (p.TryGet("clipboard", out System.Text.Json.Nodes.JsonObject? clip) && clip != null)
        {
            try
            {
                View.Paste.Clip = CopyFormat.FromJson(clip, keepSources: false);
                _keptClip = View.Paste.Clip;
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or NullReferenceException or ArgumentException or System.Text.Json.JsonException)
            {
                Log.Caught(ex, "restoring the clipboard kept in the preferences (it is dropped)");
                p.Remove("clipboard");
            }
        }
        View.Paste.Changed += () =>
        {
            if (View.Paste.Clip is { } c && !ReferenceEquals(c, _keptClip))
            {
                _keptClip = c;
                p.Set("clipboard", CopyFormat.ToJson(c));
            }
        };
    }
}
