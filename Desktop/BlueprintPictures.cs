using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Blueprints' pictures decoded once for the lists that show them again at every search keystroke: a picture file by
/// its path while its last write time stays the same, a picture kept in a blueprint (a data URL) while that text is.
/// </summary>
internal static class BlueprintPictures
{
    /// <summary>The picture files decoded, by path: the write time read with it and the picture (null: unreadable).</summary>
    private static readonly Dictionary<string, (DateTime Written, Bitmap? Picture)> Files = [];

    /// <summary>The data URLs decoded, by the text itself (a blueprint's own, gone with it).</summary>
    private static readonly ConditionalWeakTable<string, StrongBox<Bitmap?>> Urls = [];

    /// <summary>Guards <see cref="Files"/>.</summary>
    private static readonly Lock FilesLock = new();

    /// <summary>A picture file, null when there is none or it cannot be read; decoded again only once it changes.</summary>
    public static Bitmap? File(string? path)
    {
        if (path == null)
        {
            return null;
        }
        DateTime written = System.IO.File.GetLastWriteTimeUtc(path);
        lock (FilesLock)
        {
            if (Files.TryGetValue(path, out var kept) && kept.Written == written)
            {
                return kept.Picture;
            }
        }
        Bitmap? picture;
        try
        {
            using var s = System.IO.File.OpenRead(path);
            picture = new Bitmap(s);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Log.CaughtBrief(ex, "reading a blueprint's picture file (shown without it)");
            picture = null;
        }
        lock (FilesLock)
        {
            Files[path] = (written, picture);
        }
        return picture;
    }

    /// <summary>A picture kept as a PNG data URL, null when it is none or cannot be read; decoded once per text.</summary>
    public static Bitmap? Url(string? dataUrl)
    {
        if (dataUrl == null || !dataUrl.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
        {
            return null;
        }
        return Urls.GetValue(dataUrl, Decode).Value;
    }

    /// <summary>Decodes a PNG data URL (null when it cannot be read).</summary>
    private static StrongBox<Bitmap?> Decode(string dataUrl)
    {
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(dataUrl["data:image/png;base64,".Length..]));
            return new(new Bitmap(ms));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            Log.CaughtBrief(ex, "reading the picture kept in a blueprint (shown without it)");
            return new(null);
        }
    }
}
