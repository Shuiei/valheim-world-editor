namespace TerrainEditor.App;

// Writing a file the app keeps (settings, servers): to a temporary file next to it, then moved over
// it, so a crash or a power cut halfway leaves the old file whole, never a cut one. With a Unix mode,
// the file has it from the start (no moment where others can read a saved password).
public static class SafeFile
{
	public static void WriteAllText(string path, string text, UnixFileMode? mode = null)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		string temp = path + ".tmp";
		var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
		if (mode is { } m && !OperatingSystem.IsWindows())
		{
			options.UnixCreateMode = m;
		}
		File.Delete(temp);
		using (var stream = new FileStream(temp, options))
		using (var writer = new StreamWriter(stream))
		{
			writer.Write(text);
			writer.Flush();
			stream.Flush(flushToDisk: true);
		}
		File.Move(temp, path, overwrite: true);
	}
}
