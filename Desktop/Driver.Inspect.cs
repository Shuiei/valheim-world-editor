using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The inspect command: an object of a prefab is selected and its inspector opened.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'inspect' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdInspect(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        // inspect <prefab>: the first such object selected, its inspector open.
        var scene = w.View.Scene ?? throw new InvalidOperationException("no area open");
        // One holding something first (a chest with contents).
        var all = Enumerable.Range(0, scene.Things.Count).Where(k => !scene.Things[k].Gone && scene.Things[k].Prefab == Save.StableHash.Of(a[1])).ToList();
        int i = all.Cast<int?>().FirstOrDefault(k => scene.Session is { } ss && ObjectData.Bytes(scene.World, ss.Edits, scene.Things[k!.Value].Id) is { } b
            && Save.ZdoData.Parse(b).GetBytes(ObjectData.ItemsKey) != null) ?? (all.Count > 0 ? all[0] : -1);
        if (i < 0)
        {
            throw new InvalidOperationException($"no {a[1]} here");
        }
        w.Tools.ChooseMode(ToolMode.Select);
        w.View.Select([i]);
        w.View.Focus(scene.Things[i].Position);
        w.Inspect();
        return Task.FromResult(State(w));
    }
}
