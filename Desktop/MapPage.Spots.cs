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
        string biome = _session != null ? ZoneFilter.BiomeName(_session.Terrain.BiomeAt(zx * 64f, zz * 64f), _session.Terrain.Rules.Biomes).Shown : "";
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
    /// The mouse is over (x, z): shows the point, its zone and its biome, then its territory, alt biomes and status
    /// effects when it has any.
    /// </summary>
    private void Hover(float x, float z)
    {
        if (_session is { } session)
        {
            _hovered = (x, z);
            Cursor.Text = HoverText(session, x, z);
        }
    }

    /// <summary>The mouse over a world point (the driver's): the line <see cref="Hover"/> shows.</summary>
    internal string HoverAt(float x, float z)
    {
        Hover(x, z);
        return Cursor.Text ?? "";
    }

    /// <summary>The cursor line of a point: where, its zone, its biome and <see cref="SpotExtras"/>.</summary>
    private string HoverText(WorldSession session, float x, float z)
    {
        var (zx, zz) = Save.ChunkMath.SectorOf(x, z);
        var terrain = session.Terrain;
        return Lang.T("map.label.cursor", x, z, zx, zz, ZoneFilter.BiomeName(terrain.BiomeAt(x, z), terrain.Rules.Biomes).Shown)
            + SpotExtras(session, x, z);
    }
}
