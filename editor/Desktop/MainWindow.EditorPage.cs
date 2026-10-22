using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// An object the map's search found, when the area was opened around its zone: selected, the camera on it (items
    /// and texts: in the inspector).
    /// </summary>
    /// <param name="scene">The area just shown.</param>
    /// <param name="spot">The map's chosen zone the area was opened around, if it was.</param>
    private void FocusSearchTarget(WorldScene scene, (int X, int Z)? spot)
    {
        if (MapPage?.Target is not var (id, inspect) || spot == null || MapPage.Spot != spot)
        {
            return;
        }
        int i = scene.Things.FindIndex(t => t.Id == id && !t.Gone);
        if (i < 0)
        {
            return;
        }
        View.Focus(scene.Things[i].Position);
        View.Select([i]);
        if (inspect)
        {
            Inspect();
        }
    }

    /// <summary>
    /// Awaited before an area's zones are read (tests hold loads there to order them).
    /// </summary>
    internal Func<Task> BeforeLoad { get; set; } = () => Task.CompletedTask;

    /// <summary>
    /// Shows a loaded area (or the Workshop's plot) in the 3D editor, the game's models opened first if they are not
    /// yet.
    /// </summary>
    /// <param name="scene">The area or plot.</param>
    /// <param name="stillWanted">Asked once the models are open: false, nothing is shown.</param>
    /// <returns>Whether it was shown.</returns>
    internal async Task<bool> ShowEditor(WorldScene scene, Func<bool>? stillWanted = null)
    {
        if (_models == null && _modelsLoading != null)
        {
            _models = await _modelsLoading;
            _modelsLoading = null;
        }
        _models ??= await Task.Run(ModelStore.Open);
        if (stillWanted?.Invoke() == false)
        {
            return false;
        }
        ResetWorkshop();
        // Lines and shapes drawn in the last area are left behind.
        View.Path.Clear();
        View.Area.Clear();
        View.Tape.Clear();
        PlaceTool.ClearShape();
        Inspector.Close();
        LoadedInfo.Text = scene.LoadInfo + (_models == null ? "\n" + Lang.T("world.msg.noModels") : "");
        View.Show(scene, _models);
        // The running extensions' tools (the world's mods were read as it opened).
        Tools.SetModTools(ModExtensions.Tools);
        if (scene.Session != null)
        {
            Edit(scene.Session);
        }
        // The Area tool's lists follow the scene shown (its selection was cleared before, with the old one shown).
        AreaPanel.Refresh();
        MapButton.IsVisible = scene.Owner != null && !Options.Direct;
        TitleText.Text = scene.Name;
        SubtitleText.Text = Lang.T("world.areaInfo", scene.ZonesX, scene.ZonesZ, scene.X0, scene.Z0, scene.X0 + scene.ZonesX - 1, scene.Z0 + scene.ZonesZ - 1, scene.Things.Count(t => !t.Gone));
        // Read whole on hover where a narrow window ends it in "…".
        ToolTip.SetTip(SubtitleText, SubtitleText.Text);
        Title = $"{scene.Name} · Valheim World Editor {BuildInfo.Version}";
        _pages.Content = _editorPage;
        EditorOpened(scene);
        return true;
    }
}
