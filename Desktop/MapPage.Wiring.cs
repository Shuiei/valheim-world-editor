using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map page's events: the buttons, the map options, the Edit button and the map's own events, wired to the page.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>Wires the page's buttons and the map options to what they do.</summary>
    /// <param name="worlds">The Worlds button.</param>
    private void WireButtons(Button worlds)
    {
        worlds.Click += (_, _) => BackToWorlds?.Invoke();
        SaveButton.Click += (_, _) => SaveRequested?.Invoke();
        DiscardButton.Click += (_, _) => DiscardRequested?.Invoke();
        ReloadButton.Click += (_, _) => ReloadRequested?.Invoke();
        ReloadButton.Tip("map.reload");
        BuildingsBox.Tip("map.buildings");
        BuildingsBox.IsCheckedChanged += (_, _) => { Map.ShowBuildings = BuildingsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        RuinsBox.Tip("map.ruins");
        RuinsBox.IsCheckedChanged += (_, _) => { Map.ShowRuins = RuinsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        GridBox.IsCheckedChanged += (_, _) => { Map.ShowGrid = GridBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        EditedBox.IsCheckedChanged += (_, _) => { Map.ShowEdited = EditedBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        PaintBox.IsCheckedChanged += (_, _) => { Map.ShowPaint = PaintBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        CloudsBox.IsCheckedChanged += (_, _) => { Map.ShowClouds = CloudsBox.IsChecked == true; Map.RequestNextFrameRendering(); };
        EditButton.Click += (_, _) => { if (Spot is var (x, z)) { EditRequested?.Invoke(x, z, Size); } };
        SizeBox.SelectionChanged += (_, _) => ShowChosen();
        SizeBox.Tip("map.size");
    }

    /// <summary>Wires the map's events: a click picks a zone, hovering shows the point, and the status and labels follow the map.</summary>
    private void WireMap()
    {
        Map.Picked += (x, z) => { var (zx, zz) = Save.ChunkMath.SectorOf(x, z); Pick(zx, zz); };
        Map.Hovered += Hover;
        Map.Status += t => _status.Text = t;
        Map.PiecesRead += () => BuildingsBox.Content = Lang.T("map.label.buildingsCount", Map.PieceCount);
        Map.ViewChanged += PlaceLabels;
        Map.SizeChanged += (_, _) => PlaceLabels();
    }
}
