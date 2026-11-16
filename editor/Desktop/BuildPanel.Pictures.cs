using System.Numerics;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace TerrainEditor.Desktop;

public sealed partial class BuildPanel
{
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
