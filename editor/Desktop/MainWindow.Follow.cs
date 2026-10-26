using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Follow: when the view's centre comes within 12 m of the area's edge, the area grows by a row of zones on that
    /// side (both sides near a corner), keeping every zone it has, up to <c>MaxFollowZones</c> each way. The new
    /// block (south-west zone and size), or null.
    /// </summary>
    internal async Task<(int X0, int Z0, int ZonesX, int ZonesZ)?> CheckFollow()
    {
        if (_followBusy || !Settings.AreaFollow || _pages.Content != _editorPage || View.Scene is not { Owner: not null } s)
        {
            return null;
        }
        var t = View.Camera.Target;
        bool nearX = MathF.Abs(t.X) > (s.W - 1) / 2f - 12, nearZ = MathF.Abs(t.Z) > (s.H - 1) / 2f - 12;
        if (!nearX && !nearZ)
        {
            _followSaid = Said.Empty;
            return null;
        }
        if (MoveBlocker() is { } why)
        {
            SayFollow(Said.Of("top.msg.followWaits", why));
            return null;
        }
        // The view mirrors z: north is -t.Z.
        if (Grown((s.X0, s.Z0, s.ZonesX, s.ZonesZ), nearX ? t.X > 0 : null, nearZ ? t.Z < 0 : null) is not { } grown)
        {
            SayFollow(Said.Of("top.msg.followFull", MaxFollowZones));
            return null;
        }
        var (x0, z0, zonesX, zonesZ) = grown;
        _followBusy = true;
        try
        {
            await OpenBlockKeepingView(x0, z0, zonesX, zonesZ, progress: Said.Of("top.msg.growing"));
        }
        finally
        {
            _followBusy = false;
        }
        // A failed open leaves the old area: nothing grew.
        return View.Scene is { } ns && (ns.X0, ns.Z0, ns.ZonesX, ns.ZonesZ) == (x0, z0, zonesX, zonesZ)
            ? (x0, z0, zonesX, zonesZ)
            : null;
    }

    /// <summary>Says why Follow does nothing, once while the reason stays.</summary>
    private void SayFollow(Said text)
    {
        if (_followSaid != text)
        {
            Msg(text);
        }
        _followSaid = text;
    }
}
