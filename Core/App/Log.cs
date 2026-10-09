namespace TerrainEditor.App;

// Console output also goes to a log file in the data folder (a window app has no console on Windows),
// each line with its time. Like BepInEx's LogOutput.log, the file is started over at each run: after a
// problem it holds that run, for the person to attach to a bug report (Open log on the start page).
public static class Log
{
	public static string FilePath => Path.Combine(AppSettings.DataDir, "ValheimWorldEditor.log");

	// The log so far (it stays open for writing: read shared).
	public static string Read()
	{
		try
		{
			using var s = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var r = new StreamReader(s);
			return r.ReadToEnd();
		}
		catch (Exception e)
		{
			return $"(the log could not be read: {e.Message})";
		}
	}

	// Starts the log (a new one each run), with what is running first.
	public static void Start(string version)
	{
		try
		{
			Directory.CreateDirectory(AppSettings.DataDir);
			// Versions before 1.15.5 wrote log.txt.
			File.Delete(Path.Combine(AppSettings.DataDir, "log.txt"));
			var file = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { AutoFlush = true };
			file.WriteLine($"Valheim World Editor {version}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription} " +
				$"({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}), .NET {Environment.Version}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			file.WriteLine($"Data folder: {AppSettings.DataDir}");
			var sink = new Sink(file);
			Console.SetOut(new Tee(Console.Out, sink));
			Console.SetError(new Tee(Console.Error, sink));
		}
		catch
		{
		}
	}

	// The file, written by both the output and the error streams: the time before each line.
	private sealed class Sink(TextWriter file)
	{
		private readonly object _lock = new();
		private bool _lineStart = true;

		public void Write(string? text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			lock (_lock)
			{
				try
				{
					string[] parts = text.Replace("\r\n", "\n").Split('\n');
					for (int i = 0; i < parts.Length; i++)
					{
						if (i > 0)
						{
							file.Write('\n');
							_lineStart = true;
						}
						if (parts[i].Length == 0)
						{
							continue;
						}
						if (_lineStart)
						{
							file.Write(DateTime.Now.ToString("HH:mm:ss "));
						}
						file.Write(parts[i]);
						_lineStart = false;
					}
				}
				catch
				{
				}
			}
		}
	}

	private sealed class Tee(TextWriter console, Sink file) : TextWriter
	{
		public override System.Text.Encoding Encoding => console.Encoding;

		public override void Write(char value)
		{
			try { console.Write(value); } catch { }
			file.Write(value.ToString());
		}

		public override void Write(string? value)
		{
			try { console.Write(value); } catch { }
			file.Write(value);
		}

		public override void WriteLine(string? value)
		{
			try { console.WriteLine(value); } catch { }
			file.Write(value + "\n");
		}
	}
}
