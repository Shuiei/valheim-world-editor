using Photino.NET;

namespace TerrainEditor.App;

// The app's own window (Photino: WebView2 on Windows, WebKitGTK on Linux) showing the local editor.
// Returns false when no window can be made (missing WebView2 / WebKitGTK), so the caller opens the
// browser instead.
public static class NativeWindow
{
	private static PhotinoWindow? _open;

	// Close the window from any thread (the app is quitting).
	public static void Close()
	{
		PhotinoWindow? w = _open;
		try
		{
			w?.Invoke(() => w.Close());
		}
		catch
		{
		}
	}

	public static bool TryRun(string url, string title, Action closed)
	{
		PhotinoWindow window;
		try
		{
			window = new PhotinoWindow()
				.SetTitle(title)
				.SetUseOsDefaultSize(false)
				.SetSize(1500, 950)
				.Center()
				.SetResizable(true)
				.SetLogVerbosity(0)
				.SetDevToolsEnabled(true)
				.SetContextMenuEnabled(true);
			string icon = Path.Combine(AppContext.BaseDirectory, "wwwroot", OperatingSystem.IsWindows() ? "icon.ico" : "icon.png");
			if (File.Exists(icon))
			{
				window.SetIconFile(icon);
			}
			// Links that leave the editor (documentation, GitHub) open in the browser; "pick:<id>:<title>"
			// shows a folder dialog and answers "<id>:<folder>" (empty when cancelled).
			PhotinoWindow w = window;
			window.RegisterWebMessageReceivedHandler((_, message) =>
			{
				if (message.StartsWith("open:", StringComparison.Ordinal))
				{
					AppHost.OpenBrowser(message[5..]);
				}
				else if (message.StartsWith("pick:", StringComparison.Ordinal))
				{
					string[] parts = message.Split(':', 3);
					string picked = "";
					try
					{
						picked = w.ShowOpenFolder(parts.Length > 2 ? parts[2] : "Choose a folder")?.FirstOrDefault() ?? "";
					}
					catch (Exception ex)
					{
						Console.Error.WriteLine("Folder dialog failed: " + ex.Message);
					}
					w.SendWebMessage($"{parts[1]}:{picked}");
				}
			});
			window.Load(new Uri(url));
			_open = window;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("No app window (" + ex.Message + "); using the browser.");
			return false;
		}
		try
		{
			window.WaitForClose();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("The app window failed (" + ex.Message + "); using the browser.");
			return false;
		}
		closed();
		return true;
	}
}
