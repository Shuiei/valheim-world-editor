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
        if (poly == null)
        {
            Info.Text = Area.Box ? Lang.T("area.hint.box") : Lang.T("area.hint.poly");
            FillOnly([]);
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
        // The Only list: the objects of the chosen kinds inside, the most common first.
        FillOnly([.. inside.Where(i => Area.Kinds.Contains(KindOf(s.Things[i]))).GroupBy(i => NameOf(s.Things[i].Prefab)).OrderByDescending(x => x.Count()).Select(x => (x.Key, x.Count()))]);
        _view.LassoChanged();
    }

    /// <summary>
    /// Fills the Only list: "every chosen kind" first, then each kind of object inside with its count. The one
    /// chosen stays chosen while it is still inside (whatever its count now); otherwise the list goes back to its
    /// first entry.
    /// </summary>
    /// <param name="names">The objects inside of the chosen kinds: each name with how many there are.</param>
    private void FillOnly(List<(string Name, int Count)> names)
    {
        string? keep = OnlyName();
        _onlyNames = [.. names.Select(n => n.Name)];
        OnlyBox.ItemsSource = new[] { Lang.T("area.only.all") }.Concat(names.Select(n => $"{n.Name} ({n.Count})")).ToList();
        OnlyBox.SelectedIndex = keep == null ? 0 : _onlyNames.IndexOf(keep) + 1;
    }

    /// <summary>The one kind of object the Only list narrows Select objects to; null: every object of the chosen kinds.</summary>
    internal string? OnlyName() => OnlyBox.SelectedIndex >= 1 && OnlyBox.SelectedIndex <= _onlyNames.Count ? _onlyNames[OnlyBox.SelectedIndex - 1] : null;

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
