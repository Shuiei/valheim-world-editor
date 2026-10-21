using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    // ---- Heightmaps: the area's ground as a 16-bit picture (with its lowest and highest heights), and a
    // picture back into the ground, fitted to the selection or the whole area.
    /// <summary>Where Export writes (null: the heightmaps folder in the app's data folder). Tests use their own.</summary>
    internal string? HeightmapFolder { get; set; }

    /// <summary>
    /// Writes the whole area's ground as a 16-bit PNG, with its lowest and highest heights in it, named after the
    /// world and its zones.
    /// </summary>
    /// <param name="folder">Where to write it; null: <see cref="HeightmapFolder"/>.</param>
    /// <returns>The message for the status bar, or null when no world is open.</returns>
    internal Said? ExportHeightmap(string? folder = null)
    {
        if (_view.Scene is not { World: { } world } s)
        {
            return null;
        }
        int x1 = s.X0 + s.ZonesX - 1, z1 = s.Z0 + s.ZonesZ - 1;
        byte[] png = Heightmaps.Encode(s.W, s.H, s.Heights, $"{world.Name} zones {s.X0},{s.Z0} to {x1},{z1}", out float min, out float max);
        string dir = folder ?? HeightmapFolder ?? Path.Combine(AppSettings.DataDir, "heightmaps");
        string safe = string.Concat(world.Name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        string path = Path.Combine(dir, $"{safe}_{s.X0}_{s.Z0}_{x1}_{z1}.png");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "writing the heightmap picture");
            return Said.Of("area.msg.heightmapFailed", path, ex.Message);
        }
        return Said.Of("area.msg.heightmapWritten", path, s.W, s.H, min, max);
    }

    /// <summary>
    /// Puts the picture read into the ground, from Lowest (black) to Highest (white), faded by the soft edge.
    /// Locked points are skipped; points the game's ±8 m keeps from their height are counted in the message.
    /// </summary>
    internal void PutPicture()
    {
        if (_picture is not { } p || _session() is not { } session)
        {
            return;
        }
        if (p.Ground != session.Ground)
        {
            // Fitted to another area's ground: its points would land anywhere in this one.
            _picture = null;
            _pictureBox.IsVisible = false;
            Say(Said.Of("area.msg.pictureOtherArea"));
            return;
        }
        float lo = Value(LowestBox), hi = Value(HighestBox);
        int clamped = 0;
        var touched = session.EditGround(Lang.T("area.undo.heightmap"), g =>
        {
            var (list, c) = PutHeights(g, p.Values, p.X0, p.Z0, p.W, p.H, p.Cells, lo, hi);
            clamped = c;
            return (list, (0, 0, g.W - 1, g.H - 1));
        });
        _picture = null;
        _pictureBox.IsVisible = false;
        Say(clamped > 0 ? Said.Of("area.msg.importClamped", clamped) : Said.Of("area.msg.imported", touched.Count));
    }

    /// <summary>
    /// The selection changed: a picture read was fitted to the old one, so it is dropped, unless the outline is still
    /// the same (a polygon closed with Enter, a box redrawn where it is).
    /// </summary>
    private void DropPicture()
    {
        if (_picture is not { } p)
        {
            return;
        }
        var now = Area.Polygon();
        if (p.Fit == null ? now == null : now != null && p.Fit.SequenceEqual(now))
        {
            return;
        }
        _picture = null;
        _pictureBox.IsVisible = false;
        Say(Said.Of("area.msg.pictureDropped"));
    }
}
