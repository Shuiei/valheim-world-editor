using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The test driver's quit: what is pending is dropped without asking.
    /// </summary>
    internal void CloseWithoutAsking()
    {
        _closeAnyway = true;
        Close();
    }

    /// <summary>
    /// Starts editing a loaded scene (also used by tests with a scene of their own).
    /// </summary>
    internal void Edit(EditSession session)
    {
        // Another area: building in the last one's dungeon (and its cut) ends, and its placed dungeon is no more to go to.
        if (BuildingInDungeon)
        {
            BuildInDungeon(false);
        }
        LastPlaced = null;
        Session = session;
        // The Dungeon tool's rooms and dungeons: the game's, or as this world's Expand World Data files make them.
        Editing.Dungeons.UseMod(session.Scene.Terrain?.Rules.Dungeons);
        session.Brush = Tools.Brush;
        MountainPanel.FitTo(session.Ground.W);
        if (MountainPanel.Spec.Radius * 1.15f > (session.Ground.W - 1) / 2f - 2)
        {
            MountainPanel.Randomize();
        }
        session.Ground.NoLimit = Tools.NoLimitBox.IsChecked == true;
        if (session.Scene.World is { } bw)
        {
            // The builder chosen for this world before, else the world's main builder.
            TerrainEditor.Save.WorldSave.Builder = PlacePanel.Memory.Builders.TryGetValue(bw.Name, out long known) ? known : Builders.Default(bw);
            FillBuilders();
        }
        session.Mask = Mask;
        // New objects keep a green marker until saved, or until the game has them (live).
        View.InGame = id => session.Scene.Owner is { } owner && (owner.IsLive ? owner.LiveSync.IsLive(id) : owner.IsSaved(id));
        // What is placed stays visible: its kinds switched off in View are switched on.
        session.ThingsAdded += indices => Dispatcher.UIThread.Post(() => ShowKindsOf(session.Scene, indices));
        session.Changed += () => Dispatcher.UIThread.Post(() =>
        {
            QueueSupport();
            UpdateSaveBar();
            History.Refresh();
            // The dungeon's rooms changed (an edit, undo): the Dungeon tool's list follows.
            if (Tools.Mode == ToolMode.Dungeon)
            {
                DungeonPanel.Reload();
            }
            // The ground changed: the Area tool's cut and fill follows.
            if (Tools.Mode == ToolMode.Area)
            {
                AreaPanel.RefreshVolume();
            }
            // And the tape's lowest and highest ground.
            if (Tools.Mode == ToolMode.Measure)
            {
                MeasurePanel.Refresh();
            }
        });
        session.EditMade += () => Dispatcher.UIThread.Post(() => AutoApply(session));
        // Saved: the objects were read again, with new indices.
        session.ThingsReset += () => Dispatcher.UIThread.Post(() => { if (Inspector.IsOpen) { Inspector.Close(); } });
        // The kinds the Select tool can replace with.
        var kinds = session.Scene.World?.Creatable.Where(p => NameOfPrefab(p) != null).OrderBy(NameOfPrefab, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        SelectPanel.ReplaceKinds = kinds;
        SelectPanel.ReplaceBox.ItemsSource = kinds.Select(p => NameOfPrefab(p)!).ToList();
        SelectPanel.FillSaved();
        UpdateSaveBar();
    }

    /// <summary>The save bar's pill while something waits: amber background, border and text.</summary>
    private static readonly IImmutableSolidColorBrush PillWaiting = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#2c2416")),
        PillWaitingBorder = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7a5a2a")), PillWaitingText = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#f3d29b"));
}
