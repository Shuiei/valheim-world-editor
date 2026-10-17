using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>The action (Apply, Enter). While one runs, another does nothing (Apply greyed out).</summary>
    public async Task Apply()
    {
        if (_applying)
        {
            return;
        }
        _applying = true;
        ApplyButton.IsEnabled = false;
        try
        {
            await ApplyOnce();
        }
        finally
        {
            _applying = false;
            ApplyButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Whether the session is still the one open after a wait; when it is not (another area opened, the world
    /// closed), says that nothing was applied.
    /// </summary>
    /// <param name="session">The session the action started on.</param>
    private bool StillOpen(EditSession session)
    {
        if (ReferenceEquals(_session(), session))
        {
            return true;
        }
        Say(Said.Of("area.msg.changed"));
        return false;
    }

    /// <inheritdoc cref="Apply"/>
    private async Task ApplyOnce()
    {
        if (_session() is not { } session)
        {
            return;
        }
        var poly = Area.Polygon();
        if (poly == null)
        {
            Say(Said.Of("paste.msg.areaFirst"));
            return;
        }
        var act = Current;
        if (Ground(act) is { } ga)
        {
            ApplyGround(session, act, ga);
            Refresh();
            return;
        }
        if (await ApplyObjectAction(session, poly, act))
        {
            Refresh();
        }
    }

    /// <summary>Runs an object action on the selection.</summary>
    /// <returns>Whether the panel refreshes afterwards: false when the action had nothing to work on (it says so).</returns>
    private async Task<bool> ApplyObjectAction(EditSession session, List<Vector2> poly, Act act)
    {
        switch (act)
        {
            case Act.Remove:
                return RemoveInside(session, poly);
            case Act.Select:
                _view.Select(ThingsInside(poly, chosenKindsOnly: true));
                SwitchToSelect?.Invoke();
                return true;
            case Act.Replace:
                return ReplaceChosen(session, poly);
            case Act.Reset:
                await ResetZones(session);
                return true;
            case Act.Regrow:
                await RegrowNature(session, poly);
                return true;
            case Act.Backup:
                await RestoreBackup(session, poly);
                return true;
            default:
                return true;
        }
    }
}
