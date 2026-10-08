namespace TerrainEditor.Desktop;

// A picture of the whole window as the screen shows it, panels and OpenGL views together (the
// documentation's screenshots, through the test driver): a snapshot taken by Avalonia's compositor,
// inside the app, never a capture of the screen.
public static class WindowShot
{
	public static async Task Save(Avalonia.Controls.Window window, string path)
	{
		var visual = Avalonia.Rendering.Composition.ElementComposition.GetElementVisual(window) ?? throw new InvalidOperationException("The window is not shown.");
		using var bitmap = await visual.Compositor.CreateCompositionVisualSnapshot(visual, window.RenderScaling);
		bitmap.Save(path);
	}
}
