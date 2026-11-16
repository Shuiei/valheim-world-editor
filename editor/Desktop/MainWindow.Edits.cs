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
            // The Build tool stays on: over the new area's open ground now.
            BuildInWorld(Tools.BuildMode && !InWorkshop);
        }
        // The last area's dungeon is left behind: unless the Dungeon tool is on (it finds this area's dungeons), the
        // view's cut counts from the ground again.
        if (ViewCut.From == ViewCut.Level.Dungeon && Tools.Mode != ToolMode.Dungeon)
        {
            ViewCut.FromGround();
        }
        LastPlaced = null;
        Session = session;
        // The Dungeon tool's rooms and dungeons: the game's, or as this world's Expand World Data files make them.
        Editing.Dungeons.UseMod(session.Scene.Terrain?.Rules.Dungeons);
        session.Brush = Tools.Brush;
        LandformPanel.Mountain.FitTo(session.Ground.W);
        if (LandformPanel.Mountain.Spec.Radius * 1.15f > (session.Ground.W - 1) / 2f - 2)
        {
            LandformPanel.Mountain.Randomize();
        }
        session.Ground.NoLimit = MaskPanel.NoLimitBox.IsChecked == true;
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
        session.Changed += () => Dispatcher.UIThread.Post(() => AfterChange(session));
        session.EditMade += () => Dispatcher.UIThread.Post(() => AutoApply(session));
        // Saved: the objects were read again, with new indices.
        session.ThingsReset += () => Dispatcher.UIThread.Post(() => { if (Inspector.IsOpen) { Inspector.Close(); } });
        // The kinds the Select tool can replace with, for this session's world.
        _replaceKindsFrom = null;
        FillReplaceKinds();
        SelectPanel.FillSaved();
        UpdateSaveBar();
    }

    /// <summary>
    /// After a change of the session edited (an edit, an undo, a redo): the support check, the save bar, the object
    /// count under the world's name, the history, the dungeon last placed and the tool's own readouts follow.
    /// </summary>
    /// <param name="session">The session that changed.</param>
    private void AfterChange(EditSession session)
    {
        QueueSupport();
        UpdateSaveBar();
        // The object count under the world's name follows what was placed, deleted or undone.
        if (!InWorkshop && Session == session)
        {
            ShowAreaInfo(session.Scene);
        }
        History.Refresh();
        // The dungeon last placed may have been undone or redone.
        FollowPlaced();
        RefreshToolAfterChange();
    }

    /// <summary>
    /// The tool in use's readouts after a change: the Dungeon tool's list of rooms, the Area tool's cut and fill, the
    /// tape's lowest and highest ground.
    /// </summary>
    private void RefreshToolAfterChange()
    {
        switch (Tools.Mode)
        {
            case ToolMode.Dungeon:
                DungeonPanel.Reload();
                break;
            case ToolMode.Area:
                AreaPanel.RefreshVolume();
                break;
            case ToolMode.Measure:
                MeasurePanel.Refresh();
                break;
            default:
                // The other tools show nothing that a change makes stale.
                break;
        }
    }

    /// <summary>The save bar's pill while something waits: amber background, border and text.</summary>
    private static readonly IImmutableSolidColorBrush PillWaiting = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#2c2416")),
        PillWaitingBorder = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#7a5a2a")), PillWaitingText = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#f3d29b"));
}
