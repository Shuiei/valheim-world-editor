using System.Text.Json;
using Avalonia.Threading;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// What Claude can do in the editor (ClaudeServer): look at what is open, and change it as the user would,
/// on the window's thread. Coordinates are the game's: x east, z north, y up, in metres. Every change
/// stays pending, one step of the history each: the user saves, applies live or saves the blueprint.
/// </summary>
/// <remarks>
/// Each tool's description for Claude is its <c>[Description]</c> attribute; the summaries here are for the code.
/// </remarks>
[McpServerToolType]
public sealed partial class ClaudeTools
{
    /// <summary>The editor's window, whose world, session and view the tools work on.</summary>
    private readonly MainWindow _w;

    /// <summary>The tools for this window.</summary>
    internal ClaudeTools(MainWindow window)
    {
        _w = window;
        ViewPicture = _w.View.Picture;
        MapPicture = path => _w.MapPage!.Map.Picture(path);
    }

    /// <summary>Saves a picture of the 3D view as a PNG file (the view's, drawn by OpenGL; tests give their own).</summary>
    internal Func<string, Task> ViewPicture { get; set; }

    /// <summary>Saves a picture of the world's map as a PNG file (the map's, drawn by OpenGL; tests give their own).</summary>
    internal Func<string, Task> MapPicture { get; set; }

    /// <summary>The worlds folders create_world may make a world in (the game's that exist; tests give their own).</summary>
    internal Func<List<string>> WorldFolders { get; set; } = NewWorldPage.Roots;

    /// <summary>
    /// The whole-world search search_world runs away from the window's thread (WorldSearch.Search; tests hold it to
    /// change the world while it runs).
    /// </summary>
    internal Func<Save.WorldSave, Editing.EditStore, string, string, WorldSearch.Result> SearchFiles { get; set; } =
        (world, edits, query, what) => WorldSearch.Search(world, edits, query, what);

    /// <summary>Compact JSON for the answers.</summary>
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>An answer as compact JSON.</summary>
    internal static string ToJson(object o) => JsonSerializer.Serialize(o, Json);

    /// <summary>On the window's thread, as a click would be.</summary>
    private static Task<T> OnUi<T>(Func<Task<T>> f) => Dispatcher.UIThread.InvokeAsync(f);

    /// <summary>On the window's thread, as a click would be.</summary>
    private static Task<T> OnUi<T>(Func<T> f) => Dispatcher.UIThread.InvokeAsync(f).GetTask();

    /// <summary>
    /// The camera tool running now (done when none is): one at a time moves the camera or takes a picture, since
    /// Claude's calls run side by side, and a picture awaited after the camera was set would otherwise come from
    /// another call's camera. Used on the window's thread only.
    /// </summary>
    private Task _camera = Task.CompletedTask;

    /// <summary>On the window's thread, the camera this call's alone until it ends (<see cref="_camera"/>).</summary>
    private Task<T> WithCamera<T>(Func<Task<T>> f) => OnUi(async () =>
    {
        while (!_camera.IsCompleted)
        {
            await _camera;
        }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _camera = done.Task;
        try
        {
            return await f();
        }
        finally
        {
            done.SetResult();
        }
    });

    /// <summary>On the window's thread, the camera this call's alone until it ends (<see cref="_camera"/>).</summary>
    private Task<T> WithCamera<T>(Func<T> f) => WithCamera(() => Task.FromResult(f()));
}
