using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The area made a square of <paramref name="size"/> zones around its middle zone: the edits, the history and the
    /// view stay.
    /// </summary>
    internal async Task ResizeArea(int size)
    {
        if (View.Scene is not { Owner: not null } s || (s.ZonesX == size && s.ZonesZ == size))
        {
            return;
        }
        if (MoveBlocker() is { } why)
        {
            Msg("top.msg.cannotResize", why);
            ShowAreaSize(s.ZonesX, s.ZonesZ);
            return;
        }
        int mx = s.X0 + s.ZonesX / 2, mz = s.Z0 + s.ZonesZ / 2;
        Msg("top.msg.opening", size);
        await OpenBlockKeepingView(mx - size / 2, mz - size / 2, size, size);
    }

    /// <summary>Shows the area's size in the size box without resizing.</summary>
    private void ShowAreaSize(int zonesX, int zonesZ)
    {
        _fillingSize = true;
        AreaSizeBox.SelectedIndex = zonesX == zonesZ ? Array.IndexOf(AreaSizes, zonesX) : -1;
        // A size not in the list (1 × 1 from the command line, or grown by Follow) shows as it is.
        AreaSizeBox.PlaceholderText = $"{zonesX} × {zonesZ}";
        _fillingSize = false;
    }

    /// <summary>Turns Follow on or off and keeps the choice in the settings.</summary>
    internal void SetFollow(bool on)
    {
        Settings.AreaFollow = on;
        SaveSettings(Settings);
        FollowButton.Classes.Set("on", on);
        Msg(on ? Said.Of("top.msg.followOn") : Said.Of("top.msg.followOff"));
        _followSaid = Said.Empty;
    }

    /// <summary>What stops the area from moving now (Follow waits): something unfinished would be lost.</summary>
    internal Said? MoveBlocker()
    {
        // First: while saving, the session's other checks would wait for the save to end.
        if (IsBusy)
        {
            return Said.Of("top.blocker.busy");
        }
        if (View.Selected.Count > 0)
        {
            return Said.Of("top.blocker.selected");
        }
        if (Session?.Stroking == true)
        {
            return Said.Of("top.blocker.stroke");
        }
        if (View.Path.Points.Count > 0)
        {
            return Said.Of("top.blocker.path");
        }
        if (_applying)
        {
            return Said.Of("top.blocker.sending");
        }
        return null;
    }
}
