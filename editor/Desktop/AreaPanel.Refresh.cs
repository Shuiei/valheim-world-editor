using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>A number box's value, 0 when empty.</summary>
    private static float Value(NumericUpDown b) => (float)(b.Value ?? 0);

    /// <summary>How many times <see cref="Refresh"/> ran: a test seam (a box dragged refreshes once per batch of moves).</summary>
    internal int Refreshes { get; private set; }

    /// <summary>The selection changed: its size, what is inside, the lists and the volumes.</summary>
    public void Refresh()
    {
        Refreshes++;
        _refreshPosted = false;
        // The backup read last goes with its world: kept only while a world of its seed is shown.
        if (_backup is { } kept && (_view.Scene?.World is not { } shown || shown.Seed != kept.World.Seed || shown.SeedName != kept.World.SeedName))
        {
            ForgetBackup();
        }
        var poly = Area.Polygon();
        RefreshVolume();
        // Before the selection's lists: the "with" list follows the world, selection or not.
        FillCreatable();
        if (poly == null)
        {
            Info.Text = Area.Box ? Lang.T("area.hint.box") : Lang.T("area.hint.poly");
            _fromNames = [];
            FromBox.ItemsSource = Array.Empty<string>();
            foreach (var (k, b) in KindButtons)
            {
                b.Content = ObjectKinds.Label(k);
            }
            _view.LassoChanged();
            return;
        }
        var inside = ThingsInside(poly, chosenKindsOnly: false);
        var s = _view.Scene!;
        Info.Text = Lang.T("area.info", AreaTool.Area(poly), inside.Count);
        var byKind = inside.GroupBy(i => KindOf(s.Things[i])).ToDictionary(x => x.Key, x => x.Count());
        foreach (var (k, b) in KindButtons)
        {
            b.Content = _view.IsShown(k) ? Lang.T("area.label.kindCount", ObjectKinds.Label(k), byKind.GetValueOrDefault(k)) : Lang.T("area.label.kindHidden", ObjectKinds.Label(k));
        }
        var kinds = inside.GroupBy(i => NameOf(s.Things[i].Prefab)).OrderByDescending(x => x.Count()).ToList();
        var names = kinds.Select(x => $"{x.Key} ({x.Count()})").ToList();
        _fromNames = [.. kinds.Select(x => x.Key)];
        FromBox.ItemsSource = names;
        FromBox.SelectedIndex = FromBox.SelectedItem is string keep && names.Contains(keep) ? names.IndexOf(keep) : names.Count > 0 ? 0 : -1;
        _view.LassoChanged();
    }

    /// <summary>The ground action behind an action, null for the object and other actions.</summary>
    private static AreaTool.GroundAction? Ground(Act a) => a switch
    {
        Act.Flatten => AreaTool.GroundAction.Flatten,
        Act.Raise => AreaTool.GroundAction.Raise,
        Act.Lower => AreaTool.GroundAction.Lower,
        Act.Smooth => AreaTool.GroundAction.Smooth,
        Act.Natural => AreaTool.GroundAction.Natural,
        Act.Restore => AreaTool.GroundAction.Restore,
        Act.Erode => AreaTool.GroundAction.Erode,
        Act.Paint => AreaTool.GroundAction.Paint,
        _ => null,
    };
}
