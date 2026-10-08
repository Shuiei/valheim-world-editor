using SkiaSharp;

namespace TerrainEditor.Desktop;

// A picture of the whole window as the screen shows it, panels and OpenGL views together (the
// documentation's screenshots, through the test driver): a snapshot taken by Avalonia's compositor,
// inside the app, never a capture of the screen. An open dialog (Settings, a question) is drawn
// over it, centred, the window dimmed behind it as it looks while the dialog waits.
public static class WindowShot
{
	public static async Task Save(Avalonia.Controls.Window window, string path)
	{
		using var main = await Snapshot(window);
		var dialogs = window.OwnedWindows.Where(d => d.IsVisible).ToList();
		if (dialogs.Count == 0)
		{
			main.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
			return;
		}
		using var canvasBitmap = Decode(main);
		using var canvas = new SKCanvas(canvasBitmap);
		canvas.DrawRect(0, 0, canvasBitmap.Width, canvasBitmap.Height, new SKPaint { Color = new SKColor(0, 0, 0, 140) });
		foreach (var d in dialogs)
		{
			using var shot = await Snapshot(d);
			using var b = Decode(shot);
			float x = (canvasBitmap.Width - b.Width) / 2f, y = (canvasBitmap.Height - b.Height) / 2f;
			canvas.DrawBitmap(b, x, y);
		}
		canvas.Flush();
		using var image = SKImage.FromBitmap(canvasBitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		using var file = File.Create(path);
		data.SaveTo(file);
	}

	private static async Task<Avalonia.Media.Imaging.Bitmap> Snapshot(Avalonia.Controls.Window window)
	{
		var visual = Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(window) ?? throw new InvalidOperationException("The window is not shown.");
		return await visual.Compositor.CreateCompositionVisualSnapshot(visual, window.RenderScaling);
	}

	private static SKBitmap Decode(Avalonia.Media.Imaging.Bitmap bitmap)
	{
		using var ms = new MemoryStream();
		bitmap.Save(ms, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
		ms.Position = 0;
		return SKBitmap.Decode(ms);
	}
}
