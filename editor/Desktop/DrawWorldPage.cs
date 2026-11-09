using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Draw a world (the start page's "A new world"): a master map drawn here with the legend's kinds only (each biome, the
/// rivers, lakes, sea and deep sea) and a start point, saved and opened as a PNG picture; and the world it makes, seen
/// from above as the editor's generator makes it (the drawing's land and biomes, the game's own shapes, rivers, lakes
/// and places on them).
/// </summary>
public sealed partial class DrawWorldPage : IDisposable
{
    /// <summary>The page.</summary>
    public Control View { get; }

    /// <summary>The Start button: back to the start page.</summary>
    public event Action? BackRequested;

    /// <summary>The map being drawn.</summary>
    internal MasterMapCanvas Canvas { get; } = new();

    /// <summary>The world the drawing makes, seen from above (the World view).</summary>
    internal SeedMap Preview { get; } = new();

    /// <summary>The maps as they were before each change, the latest last (Undo); and the changes undone (Redo).</summary>
    private readonly List<MasterMap> _undo = [], _redo = [];

    /// <summary>How many changes Undo keeps (a map is 4 MB).</summary>
    private const int UndoSteps = 30;

    /// <summary>The picture last opened or saved, or null.</summary>
    private string? _file;

    /// <summary>Whether the drawing changed since it was last opened, saved or made new.</summary>
    internal bool Unsaved { get; private set; }

    /// <summary>The drawing's stamp the World view shows, or null when it shows none.</summary>
    private string? _shownStamp;

    /// <summary>Cancels the world being made for the World view.</summary>
    private CancellationTokenSource? _making;

    /// <summary>The latest world made for the World view, for tests to wait on.</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>Builds the page, with a blank sea to draw on.</summary>
    /// <param name="roots">The world folders Create offers (tests); null for the game's own.</param>
    public DrawWorldPage(IReadOnlyList<string>? roots = null)
    {
        WireCreate(roots);
        Canvas.Changing += KeepForUndo;
        Canvas.Changed += () =>
        {
            Unsaved = true;
            FitButtons();
            ShowCounts();
        };
        Canvas.Hovered += (x, z) => Readout.Text = $"{x:0}, {z:0}";
        Preview.Hovered += (x, z) => Readout.Text = $"{x:0}, {z:0}";
        View = BuildPage();
        ShowCounts();
        FitButtons();
    }

    /// <summary>Stops the world being made and frees the pictures.</summary>
    public void Dispose()
    {
        _making?.Cancel();
        _dungeons?.Cancel();
        _checkWait?.Stop();
        Canvas.Dispose();
        Preview.Dispose();
    }

    /// <summary>Keeps the map as it is for Undo (a change is about to be made); Redo is dropped.</summary>
    private void KeepForUndo()
    {
        _undo.Add(Canvas.Map.Copy());
        if (_undo.Count > UndoSteps)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
    }

    /// <summary>The drawing as it was before the last change.</summary>
    internal void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }
        _redo.Add(Canvas.Map);
        Canvas.Show(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        Unsaved = true;
        FitButtons();
        ShowCounts();
    }

    /// <summary>The change last undone, made again.</summary>
    internal void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }
        _undo.Add(Canvas.Map);
        Canvas.Show(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        Unsaved = true;
        FitButtons();
        ShowCounts();
    }

    /// <summary>Starts again on a blank sea (Undo brings the drawing back).</summary>
    private void NewMap()
    {
        KeepForUndo();
        Canvas.Show(new MasterMap());
        _file = null;
        Unsaved = false;
        Note(null);
        FitButtons();
        ShowCounts();
    }

    /// <summary>Opens a PNG picture as the drawing (Undo brings the one before back); says what the reading noticed.</summary>
    private async Task OpenPicture()
    {
        if (TopLevel.GetTopLevel(View)?.StorageProvider is not { } storage)
        {
            return;
        }
        string? path = await Pickers.File(storage, Lang.T("masterMap.title.open"), PngType);
        if (path == null)
        {
            return;
        }
        await OpenPicture(path);
    }

    /// <summary>Opens a PNG picture as the drawing, from a path (tests, the Open button).</summary>
    internal async Task OpenPicture(string path)
    {
        MasterMap.Reading read;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);
            read = await Task.Run(() => MasterMap.Read(bytes));
            // The heights saved beside it, when there are some.
            string heights = HeightsPath(path);
            if (File.Exists(heights))
            {
                byte[] kept = await File.ReadAllBytesAsync(heights);
                read.Map.Heights = await Task.Run(() => MasterMap.ReadHeights(kept));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Caught(ex, "opening a master map");
            Note(Lang.T("masterMap.error.open", Path.GetFileName(path), ex.Message), warn: true);
            return;
        }
        KeepForUndo();
        Canvas.Show(read.Map);
        Canvas.Fit();
        _file = path;
        Unsaved = false;
        var notes = new List<string> { Lang.T("masterMap.msg.opened", Path.GetFileName(path)) };
        if (read.Unknown > 0 && read.FirstUnknown is { } first)
        {
            notes.Add(Lang.T("masterMap.msg.unknownColours", read.Unknown, first.Column, first.Row));
        }
        if (read.Starts > 1)
        {
            notes.Add(Lang.T("masterMap.msg.manyStarts", read.Starts));
        }
        Note(string.Join(" ", notes), warn: notes.Count > 1);
        FitButtons();
        ShowCounts();
    }

    /// <summary>Saves the drawing as a PNG picture, where the player chooses.</summary>
    private async Task SavePicture()
    {
        if (TopLevel.GetTopLevel(View)?.StorageProvider is not { } storage)
        {
            return;
        }
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Lang.T("masterMap.title.save"),
            SuggestedFileName = _file != null ? Path.GetFileName(_file) : "master-map.png",
            DefaultExtension = "png",
            FileTypeChoices = [PngType],
        });
        if (file?.Path is not { IsFile: true } uri)
        {
            return;
        }
        await SavePicture(uri.LocalPath);
    }

    /// <summary>Saves the drawing as a PNG picture at a path (tests, the Save button).</summary>
    internal async Task SavePicture(string path)
    {
        var map = Canvas.Map.Copy();
        try
        {
            byte[] png = await Task.Run(map.ToPng);
            await File.WriteAllBytesAsync(path, png);
            if (map.Heights != null)
            {
                await File.WriteAllBytesAsync(HeightsPath(path), await Task.Run(map.HeightsPng));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "saving a master map");
            Note(Lang.T("masterMap.error.save", Path.GetFileName(path), ex.Message), warn: true);
            return;
        }
        _file = path;
        Unsaved = false;
        Note(Lang.T("masterMap.msg.saved", Path.GetFileName(path)));
    }

    /// <summary>Where a master map's heights are saved beside it: its name with "-heights" before ".png".</summary>
    internal static string HeightsPath(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "-heights.png");

    /// <summary>
    /// Opens a heightmap as a new drawing (Undo brings the one before back): its heights kept, each cell the kind the
    /// game would give it at that height, for the seed in the seed box.
    /// </summary>
    private async Task OpenHeightmap()
    {
        if (TopLevel.GetTopLevel(View)?.StorageProvider is not { } storage)
        {
            return;
        }
        string? path = await Pickers.File(storage, Lang.T("masterMap.title.heightmap"), PngType);
        if (path != null)
        {
            await OpenHeightmap(path);
        }
    }

    /// <summary>Opens a heightmap as a new drawing, from a path (tests, the button).</summary>
    internal async Task OpenHeightmap(string path)
    {
        float sea = (float)(SeaLevel.Value ?? 10m) / 100f;
        string seed = SeedName;
        MasterMap map;
        Loading.IsVisible = true;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);
            map = await Task.Run(() => MasterMap.FromHeightmap(bytes, sea, seed));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Caught(ex, "opening a heightmap");
            Note(Lang.T("masterMap.error.open", Path.GetFileName(path), ex.Message), warn: true);
            return;
        }
        finally
        {
            Loading.IsVisible = false;
        }
        KeepForUndo();
        Canvas.Show(map);
        Canvas.Fit();
        _file = null;
        Unsaved = true;
        Note(Lang.T("masterMap.msg.heightmap", Path.GetFileName(path)));
        FitButtons();
        ShowCounts();
    }

    /// <summary>The kind of file a master map is saved as.</summary>
    private static readonly FilePickerFileType PngType = new(Lang.T("masterMap.fileType")) { Patterns = ["*.png"], MimeTypes = ["image/png"] };

    /// <summary>
    /// Shows the World view: the world the drawing makes, made again when the drawing changed since (a couple of
    /// seconds), with the start, the bosses' altars and the traders' spots marked.
    /// </summary>
    internal void ShowWorld()
    {
        Canvas.IsVisible = false;
        Preview.IsVisible = true;
        ToolsCard.IsEnabled = false;
        var map = Canvas.Map.Copy();
        string stamp = map.Stamp() + "/" + SeedName;
        if (stamp == _shownStamp)
        {
            return;
        }
        _making?.Cancel();
        var cancel = _making = new CancellationTokenSource();
        Loading.IsVisible = true;
        string seed = SeedName;
        Pending = Task.Run(() =>
        {
            var rules = MasterMapWorlds.Rules(map);
            return SeedPreview.Make(seed, 512, parallel: true, landmarks: ValheimGen.SeedLocations.Available, rules: rules, cancel: cancel.Token);
        }, cancel.Token).ContinueWith(t =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (cancel.IsCancellationRequested)
                {
                    return;
                }
                Loading.IsVisible = false;
                if (t.IsFaulted)
                {
                    var ex = t.Exception!.GetBaseException();
                    Log.Caught(ex, "making a drawn world's preview");
                    Note(Lang.T("masterMap.error.preview", ex.Message), warn: true);
                    return;
                }
                var p = t.Result;
                _shownStamp = stamp;
                Preview.Show(p);
                Preview.Dots = Landmarks(p);
                ShowWorldFacts(p);
                CheckWorld(p);
            });
        }, TaskScheduler.Default);
    }

    /// <summary>Back to the drawing.</summary>
    internal void ShowDrawing()
    {
        Preview.IsVisible = false;
        Canvas.IsVisible = true;
        ToolsCard.IsEnabled = true;
    }

    /// <summary>The seed the world's small things come from (the seed box's, or a fixed one when it is empty).</summary>
    private string SeedName => SeedBox.Text?.Trim() is { Length: > 0 and <= 10 } s ? s : "Drawn";

    /// <summary>The marks of the preview's places: the start red, the bosses' altars purple, the traders yellow.</summary>
    private static List<(float X, float Z, IBrush Fill)> Landmarks(SeedPreview.Preview p)
    {
        var marks = new List<(float, float, IBrush)>();
        foreach (var m in p.Landmarks ?? [])
        {
            marks.Add((m.X, m.Z, m.Kind switch { "start" => StartMark, "boss" => BossMark, _ => TraderMark }));
        }
        return marks;
    }

    private static readonly IBrush StartMark = new SolidColorBrush(Color.FromRgb(230, 60, 60));
    private static readonly IBrush BossMark = new SolidColorBrush(Color.FromRgb(170, 90, 230));
    private static readonly IBrush TraderMark = new SolidColorBrush(Color.FromRgb(240, 200, 60));

    /// <summary>Undo, Redo and Save lit when they can do something.</summary>
    private void FitButtons()
    {
        UndoButton.IsEnabled = _undo.Count > 0;
        RedoButton.IsEnabled = _redo.Count > 0;
    }

    /// <summary>The line under the tools (null: none); in the warning colour when it warns.</summary>
    private void Note(string? text, bool warn = false)
    {
        NoteText.Text = text ?? "";
        NoteText.IsVisible = text != null;
        NoteText.Foreground = warn ? Ui.Warn : Ui.Muted;
    }
}
