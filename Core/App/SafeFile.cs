namespace TerrainEditor.App;

/// <summary>
/// Writing a file the app keeps (settings, servers): to a temporary file next to it, then moved over
/// it, so a crash or a power cut halfway leaves the old file whole, never a cut one. With a Unix mode,
/// the file has it from the start (no moment where others can read a saved password).
/// </summary>
public static class SafeFile
{
    /// <summary>
    /// Writes the text (UTF-8) through path + ".tmp", flushed to disk before the move; makes the folder if needed.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">Its new contents.</param>
    /// <param name="mode">The Unix permissions it is created with; ignored on Windows.</param>
    /// <param name="overwrite">Whether a file there already is replaced. When not, it is left as it is and the write
    /// fails (IOException): the move is what tells, so a file put there meanwhile is never replaced.</param>
    public static void WriteAllText(string path, string text, UnixFileMode? mode = null, bool overwrite = true)
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
        try
        {
            File.Move(temp, path, overwrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Refused (a file put there meanwhile, locked, or read-only on Windows): the temporary copy, which may
            // hold a saved password, is not left behind.
            File.Delete(temp);
            throw;
        }
    }
}
