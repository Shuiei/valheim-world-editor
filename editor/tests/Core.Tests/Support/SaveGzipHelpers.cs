using System.IO.Compression;

namespace WorldEditor.Tests;

/// <summary>
/// Gzip packing and unpacking for the save tests, which build damaged or truncated gzip data by hand.
/// </summary>
public static class SaveGzipHelpers
{
    /// <summary>
    /// Unpacks gzip data.
    /// </summary>
    public static byte[] Gunzip(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Packs data as gzip.
    /// </summary>
    public static byte[] Gzip(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(raw);
        }
        return output.ToArray();
    }
}
