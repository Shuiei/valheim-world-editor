using Silk.NET.OpenGL;
using SkiaSharp;

namespace TerrainEditor.Desktop;

// What an OpenGL view just drew, saved as a PNG (the 3D view's and the map's pictures: --shot, and the
// test driver's "picture" command).
public static class GlPicture
{
	public static unsafe void Save(GL gl, int w, int h, string path)
	{
		byte[] px = new byte[w * h * 4];
		fixed (byte* p = px)
		{
			gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
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

	// A request for the next picture once the view is ready, completed from the drawing thread.
	public sealed class Request
	{
		public required string Path { get; init; }
		public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
