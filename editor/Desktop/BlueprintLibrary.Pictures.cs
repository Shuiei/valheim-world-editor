using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

internal sealed partial class BlueprintLibrary
{
    /// <summary>
    /// A blueprint without its picture (made elsewhere): one drawn from its pieces, kept in memory only
    /// (its folder is left as it is), away from the window's thread.
    /// </summary>
    private void DrawPicture(string path, Image image)
    {
        var key = (path, File.GetLastWriteTimeUtc(path));
        if (_drawn.TryGetValue(key, out var known))
        {
            image.Source = known;
            return;
        }
        if (_drawing.TryGetValue(key, out var waiting))
        {
            waiting.Add(image);
            return;
        }
        _drawing[key] = [image];
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
            _drawn[key] = b;
            if (_drawing.Remove(key, out var images))
            {
                foreach (var shown in images)
                {
                    shown.Source = b;
                }
            }
        }), TaskScheduler.Default);
    }
}
