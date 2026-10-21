using System.Numerics;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
    /// <summary>
    /// A blueprint without its picture (made elsewhere): one drawn from its pieces, kept in memory only
    /// (its folder is left as it is), away from the window's thread.
    /// </summary>
    private void DrawPicture(string path, Image image)
    {
        var key = (path, File.GetLastWriteTimeUtc(path));
        if (_blueprintPictures.TryGetValue(key, out var known))
        {
            image.Source = known;
            return;
        }
        if (_blueprintDrawing.TryGetValue(key, out var waiting))
        {
            waiting.Add(image);
            return;
        }
        _blueprintDrawing[key] = [image];
        var models = Models();
        Task.Run(() =>
        {
            try
            {
                var bp = BlueprintFormats.Parse(path, File.ReadAllText(path));
                return BuildingPicture.Draw([.. bp.Pieces.Select(p => new BuildingPicture.Piece(p.Name, p.Position, p.Euler, p.Scale))], models, 128);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.CaughtBrief(ex, "reading a blueprint to draw its picture (shown without it)");
                return null;
            }
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            Bitmap? b = null;
            if (t.Result is byte[] png)
            {
                using var ms = new MemoryStream(png);
                b = new Bitmap(ms);
            }
            _blueprintPictures[key] = b;
            if (_blueprintDrawing.Remove(key, out var images))
            {
                foreach (var shown in images)
                {
                    shown.Source = b;
                }
            }
        }), TaskScheduler.Default);
    }

    /// <summary>A piece's picture: drawn once, away from the window's thread, then shown.</summary>
    /// <returns>The picture, or null while it is being drawn (its tiles get it when done).</returns>
    private Bitmap? Picture(string prefab)
    {
        if (_pictures.TryGetValue(prefab, out var known))
        {
            return known;
        }
        if (_drawing.Add(prefab))
        {
            var models = Models();
            Task.Run(() =>
            {
                byte[] png = BuildingPicture.Draw([new BuildingPicture.Piece(prefab, Vector3.Zero, new Vector3(0, 30, 0), 0)], models, 96);
                Dispatcher.UIThread.Post(() =>
                {
                    using var ms = new MemoryStream(png);
                    _pictures[prefab] = new Bitmap(ms);
                    if (_waiting.Remove(prefab, out var images))
                    {
                        foreach (var image in images)
                        {
                            image.Source = _pictures[prefab];
                        }
                    }
                });
            });
        }
        return null;
    }
}
