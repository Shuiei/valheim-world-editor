namespace TerrainEditor.App;

// Console output also goes to a log file in the data folder (a window app has no console on Windows).
public static class Log
{
	public static string FilePath => Path.Combine(AppSettings.DataDir, "log.txt");

	// Starts the log (a new one each run), with what is running first.
	public static void Start(string version)
	{
		try
		{
			var file = new StreamWriter(FilePath, append: false) { AutoFlush = true };
			file.WriteLine($"Valheim World Editor {version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			Console.SetOut(new Tee(Console.Out, file));
			Console.SetError(new Tee(Console.Error, file));
		}
		catch
		{
		}
	}

	private sealed class Tee(TextWriter a, TextWriter b) : TextWriter
	{
		public override System.Text.Encoding Encoding => a.Encoding;

		public override void Write(char value)
		{
			try { a.Write(value); } catch { }
			try { b.Write(value); } catch { }
		}

		public override void Write(string? value)
		{
			try { a.Write(value); } catch { }
			try { b.Write(value); } catch { }
		}

		public override void WriteLine(string? value)
		{
			try { a.WriteLine(value); } catch { }
			try { b.WriteLine(value); } catch { }
		}
	}
}
