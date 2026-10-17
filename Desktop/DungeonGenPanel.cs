using Avalonia.Controls;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Generating a dungeon (the Dungeon panel's Generate): its settings, a plan of what they make (made
/// again at each change; the same settings and seed always make the same dungeon), and where it goes:
/// into the world above the view's centre, behind an entrance of its kind on the ground there (a portal
/// pair when it has none), or into the Workshop as a blueprint; and, once placed, the view up to it and
/// back to its entrance. In the Workshop (the Build panel's Generate dungeon) it is made of building
/// pieces only, and goes onto the plot: alone (Open on the plot) or with what is there (Add to the plot).
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>The settings, the plan and the summary (and, outside the Workshop, <see cref="Actions"/>).</summary>
    public Control View { get; }
    /// <summary>
    /// What the dungeon made goes to: under the settings, or (in the Workshop) where the host puts it, so
    /// that it stays in sight when the settings scroll.
    /// </summary>
    public Control Actions { get; }

    /// <summary>Place in the world: the settings and the dungeon they made.</summary>
    public event Action<DungeonGen.Settings, DungeonGen.Result>? PlaceAsked;
    /// <summary>Open in the Workshop: the settings, made of building pieces.</summary>
    public event Action<DungeonGen.Settings>? WorkshopAsked;
    /// <summary>In the Workshop: the dungeon made onto the plot (true: added to what is there).</summary>
    public event Action<DungeonGen.Settings, DungeonGen.Result, bool>? PlotAsked;
    /// <summary>Go to the dungeon: the view up to the dungeon last placed.</summary>
    public event Action? DungeonAsked;
    /// <summary>Go to the entrance: the view back down to its entrance.</summary>
    public event Action? EntranceAsked;
    /// <summary>Whether a dungeon placed in the area can be gone to changed (<see cref="Placed"/>).</summary>
    public event Action<bool>? PlacedChanged;

    /// <summary>Builds the panel; the first dungeon is made after the usual pause.</summary>
    /// <param name="inWorkshop">In the Workshop: building pieces only, and Open and Add to the plot in <see cref="Actions"/>.</param>
    public DungeonGenPanel(bool inWorkshop = false)
    {
        Defaults();
        Wire();
        (View, Actions) = BuildView(inWorkshop);
        DockPanel.SetDock(Randomize, Dock.Right);
        _wait.Tick += (_, _) => Make();
        Changed();
    }

    /// <summary>
    /// Whether a dungeon placed in the area can be gone to: the Go to buttons follow (the Dungeon panel shows them
    /// outside this panel, so they stay in sight once the dungeon made is folded away).
    /// </summary>
    public void Placed(bool yes)
    {
        ToDungeon.IsEnabled = ToEntrance.IsEnabled = yes;
        PlacedChanged?.Invoke(yes);
    }

    /// <summary>The last making failed (nothing to place): the game's data may not have been read yet.</summary>
    internal bool Failed => _result == null && !_wait.IsEnabled && Pending.IsCompleted;

    /// <summary>Makes the dungeon again for the settings shown (after the usual pause).</summary>
    internal void Remake() => Changed();
}
