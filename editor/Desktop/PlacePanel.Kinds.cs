using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The kind chooser: its search, its list, picking and ticking kinds.
public sealed partial class PlacePanel
{
    /// <summary>Shows or hides the list of kinds.</summary>
    internal Button KindsButton { get; } = new() { Content = ChooseKindsText, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };

    /// <summary>Filters the list of kinds by name.</summary>
    internal TextBox Search { get; } = new() { PlaceholderText = Lang.T("place.hint.search"), FontSize = 12 };

    /// <summary>The kinds this world can place, by category.</summary>
    internal StackPanel List { get; } = new() { Spacing = 1 };

    /// <summary>The next click on an object takes its kind (Shift + click adds it).</summary>
    internal Button PickButton { get; } = new() { Content = Lang.T("place.label.pick"), FontSize = 12 };

    /// <summary>Unchooses every kind.</summary>
    internal Button UntickAllButton { get; } = new() { Content = Lang.T("place.label.untickAll"), FontSize = 12 };

    /// <summary>
    /// The list's categories that are open (folded ones show only their header); remembered by the window.
    /// </summary>
    internal HashSet<ObjectKind> OpenKinds { get; } = [ObjectKind.Trees];

    /// <summary>A category was opened or folded (not while searching), for the window to remember.</summary>
    public event Action? OpenKindsChanged;

    /// <summary>The list's categories as last filled.</summary>
    internal Dictionary<ObjectKind, Expander> Groups { get; } = [];

    /// <summary>all / none: tick or untick every kind of the row (those this world can place).</summary>
    private void TickRow(IEnumerable<string> names, bool on)
    {
        // With no world open, every kind (nothing to check them against).
        var known = Creatable().Select(c => c.Name).ToHashSet();
        bool open = _scene()?.World != null;
        foreach (string? n in names.Where(n => !open || known.Contains(n)).ToList())
        {
            if (on && !T.Chosen.Contains(n))
            {
                T.Chosen.Add(n);
            }
            else if (!on)
            {
                T.Chosen.Remove(n);
            }
        }
        Chosen();
    }

    /// <summary>A kind chosen from a chip or the world: add (or take away if ticked), or place only it.</summary>
    internal void Choose(string name, bool add)
    {
        // Not a kind no longer offered (a chip shown before the mods' manifests or catalogue changed).
        if (_scene()?.World != null && NotOffered(name))
        {
            return;
        }
        if (!add)
        {
            T.Chosen.Clear();
            T.Chosen.Add(name);
        }
        else if (!T.Chosen.Remove(name))
        {
            T.Chosen.Add(name);
        }
        Chosen();
    }
}
