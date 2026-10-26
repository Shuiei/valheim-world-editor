using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world's portals on the map: the switch's counts and, zoomed in, their tags.
/// </summary>
public sealed partial class MapPage
{
    /// <summary>
    /// The portals' tags are written from this many metres a pixel in.
    /// </summary>
    internal const float PortalTagZoom = 8f;

    /// <summary>
    /// How far left of the map a portal's tag may still show (its text runs right of the portal), in pixels.
    /// </summary>
    private const double PortalTagRoom = 200;

    /// <summary>
    /// The portal tags' colour (the map's violet).
    /// </summary>
    private static readonly IBrush PortalTag = new SolidColorBrush(Color.FromRgb(214, 180, 255));

    /// <summary>
    /// The portals' tags, when Show portals is on and from <see cref="PortalTagZoom"/> metres a pixel in (further out
    /// they would pile up); a portal off the map gets none.
    /// </summary>
    private void PlacePortalTags()
    {
        if (!Map.ShowPortals || Map.Portals is not { } portals || Map.MetersPerPixel > PortalTagZoom)
        {
            return;
        }
        foreach (var p in portals.Portals)
        {
            // Off the map: no label (the text runs right of its portal, so one just left of the edge still shows).
            var at = Map.ScreenOf(p.X, p.Z);
            if (at.X < -PortalTagRoom || at.X > Map.Bounds.Width || at.Y < -16 || at.Y > Map.Bounds.Height + 16)
            {
                continue;
            }
            Label(p.Tag.Length > 0 ? p.Tag : Lang.T("map.portals.noTag"), p.X, p.Z, PortalTag, p.Tag.Length > 0 ? FontStyle.Normal : FontStyle.Italic);
        }
    }

    /// <summary>
    /// The portals switch's text with their counts: how many there are, how many pairs are linked and how many are alone;
    /// or why they could not be read.
    /// </summary>
    /// <param name="portals">The portals read, or null when not read yet.</param>
    /// <param name="error">Why they could not be read, or null.</param>
    internal static string PortalsLabel(PortalMap.Result? portals, string? error = null)
    {
        if (error != null)
        {
            return Lang.T("map.label.portalsError", error);
        }
        if (portals == null)
        {
            return Lang.T("map.label.portals");
        }
        int n = portals.Portals.Count;
        return n == 0 ? Lang.T("map.label.portalsNone") : Lang.T("map.label.portalsCount", n, portals.Links.Count, n - portals.Links.Count * 2);
    }
}
