namespace TerrainEditor.Desktop;

/// <summary>
/// The paste panel's control wiring: each box, slider and button writes its value to the Paste tool and notifies it.
/// </summary>
public sealed partial class PastePanel
{
    /// <summary>Connects the panel's controls to the Paste tool; the tips show on hover.</summary>
    /// <param name="paste">The Paste tool the controls write to.</param>
    private void WireControls(PasteTool paste)
    {
        GroundBox.IsCheckedChanged += (_, _) => paste.Ground = GroundBox.IsChecked == true;
        ObjectsBox.IsCheckedChanged += (_, _) => paste.Objects = ObjectsBox.IsChecked == true;
        ClearSiteBox.IsCheckedChanged += (_, _) => paste.ClearSite = ClearSiteBox.IsChecked == true;
        ClearSiteBox.Tip("paste.clearSite");
        OffsetBox.ValueChanged += (_, e) => { paste.Offset = (float)(e.NewValue ?? 0); paste.Notify(); };
        CopiesBox.ValueChanged += (_, e) => { paste.Copies = (int)(e.NewValue ?? 1); paste.Notify(); };
        AlongBox.SelectionChanged += (_, _) => { paste.Direction = (PasteTool.Along)Math.Max(0, AlongBox.SelectedIndex); paste.Notify(); };
        GapBox.ValueChanged += (_, e) => { paste.Gap = (float)(e.NewValue ?? 0); paste.Notify(); };
        TurnButton.Click += (_, _) => paste.TurnBy(90);
        MirrorButton.Click += (_, _) => { paste.Mirror = !paste.Mirror; paste.Notify(); };
        DoneButton.Click += (_, _) => Done?.Invoke();
        TurnButton.Tip("paste.turn");
        MirrorButton.Tip("paste.mirror");
        DoneButton.Tip("paste.done");
        GroundBox.Tip("paste.ground");
        ObjectsBox.Tip("paste.objects");
        OffsetBox.Tip("paste.offset");
        CopiesBox.Tip("paste.copies");
        AlongBox.Tip("paste.along");
        GapBox.Tip("paste.gap");
    }
}
