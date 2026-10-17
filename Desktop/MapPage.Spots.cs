using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The picked spot and the point under the mouse.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// A zone picked: its name, the area the 3D editor would open around it, and its edits.
    /// </summary>
    public void Pick(int zx, int zz)
    {
        Spot = (zx, zz);
        Target = null;
        string biome = _session != null ? ZoneFilter.BiomeName(_session.Terrain.BiomeAt(zx * 64f, zz * 64f)).Shown : "";
        PickTitle.Text = Lang.T("map.label.zone", zx, zz, biome);
        _pickBox.IsVisible = true;
        ShowChosen();
    }

    /// <summary>
    /// Outlines the area the 3D editor would open around the picked zone.
    /// </summary>
    private void ShowChosen()
    {
        if (Spot is var (x, z))
        {
            Map.Chosen = (x, z, Size);
            Map.RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// The mouse is over (x, z): shows the point, its zone and its biome.
    /// </summary>
    private void Hover(float x, float z)
    {
        if (_session == null)
        {
            return;
        }
        var (zx, zz) = Save.ChunkMath.SectorOf(x, z);
        Cursor.Text = Lang.T("map.label.cursor", x, z, zx, zz, ZoneFilter.BiomeName(_session.Terrain.BiomeAt(x, z)).Shown);
    }
}
