using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>
    /// Loads a blueprint onto the clipboard and starts pasting. Object ids only mean something in the
    /// world the copy came from; kinds this world cannot make are left out.
    /// </summary>
    public void Paste(string id)
    {
        if (Load(id, out Said? problem) is not var (clip, name, from, dungeon))
        {
            Message?.Invoke(problem ?? Said.Of("blueprints.msg.unreadable"));
            Refresh();
            return;
        }
        var world = _scene()?.World;
        CopyData data;
        try
        {
            data = CopyFormat.FromJson(clip, name, keepSources: world != null && from == world.Name);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            Log.CaughtBrief(ex, "reading a blueprint to paste");
            Message?.Invoke(Said.Of("blueprints.msg.unreadableWhy", ex.Message));
            Refresh();
            return;
        }
        data.Dungeon = dungeon;
        var missing = data.Objects.Where(o => world != null && !world.CanCreate(o.Prefab)).Select(o => o.Name).Distinct().ToList();
        data.Objects.RemoveAll(o => missing.Contains(o.Name));
        _paste.Clip = data;
        _paste.Notify();
        Card.IsVisible = false;
        Pasting?.Invoke();
        var onClipboard = dungeon ? Said.Of("blueprints.msg.pastingDungeon", name) : Said.Of("blueprints.msg.pasting", name);
        Message?.Invoke(onClipboard + (missing.Count > 0 ? Said.Of("blueprints.msg.unknownKinds", missing.Count, FirstFew(missing)) : Said.Empty));
    }
}
