using Silk.NET.OpenGL;
using SkiaSharp;

namespace TerrainEditor.Desktop;

/// <summary>
/// What an OpenGL view just drew, saved as a PNG (the 3D view's and the map's pictures: --shot, and the test driver's
/// "picture" command).
/// </summary>
public static class GlPicture
{
    /// <summary>
    /// Reads the current framebuffer's pixels and writes them to a PNG file, top row first. The view's context must be
    /// current.
    /// </summary>
    /// <param name="gl">The view's OpenGL.</param>
    /// <param name="w">The picture's width in pixels.</param>
    /// <param name="h">Its height in pixels.</param>
    /// <param name="path">The PNG file to write (replaced when there).</param>
    public static unsafe void Save(GL gl, int w, int h, string path)
    {
        byte[] px = new byte[w * h * 4];
        fixed (byte* p = px)
        {
            gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        Write(px, w, h, path);
    }

    /// <summary>
    /// Writes a framebuffer's pixels (RGBA, bottom row first, as OpenGL reads them) to a PNG file, top row first and
    /// opaque: the views are opaque (cleared so, and blending keeps the alpha), so an alpha under 255 left by a
    /// shader writing one with blending off would only make the picture see-through where it is not.
    /// </summary>
    /// <param name="px">The pixels, four bytes each, <paramref name="w"/> × <paramref name="h"/> of them.</param>
    /// <param name="w">The picture's width in pixels.</param>
    /// <param name="h">Its height in pixels.</param>
    /// <param name="path">The PNG file to write (replaced when there).</param>
    internal static void Write(byte[] px, int w, int h, string path)
    {
        for (int i = 3; i < px.Length; i += 4)
        {
            px[i] = 255;
        }
        using var bmp = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        // OpenGL rows go bottom up.
        for (int y = 0; y < h; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(px, (h - 1 - y) * w * 4, bmp.GetPixels() + y * w * 4, w * 4);
        }
        using var f = File.Create(path);
        bmp.Encode(f, SKEncodedImageFormat.Png, 90);
    }

    /// <summary>
    /// A request for the next picture once the view is ready, completed from the drawing thread.
    /// </summary>
    public sealed class Request
    {
        /// <summary>
        /// The PNG file to write.
        /// </summary>
        public required string Path { get; init; }
        /// <summary>
        /// Completed once the picture is written (its continuations do not run on the drawing thread).
        /// </summary>
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
