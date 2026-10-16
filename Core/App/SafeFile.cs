namespace TerrainEditor.App;

/// <summary>
/// Writing a file the app keeps (settings, servers): to a temporary file next to it, then moved over
/// it, so a crash or a power cut halfway leaves the old file whole, never a cut one. With a Unix mode,
/// the file has it from the start (no moment where others can read a saved password).
/// </summary>
public static class SafeFile
{
    /// <summary>
    /// The names Windows keeps for devices, which no file may have, with any extension (the superscript digits
    /// count as digits there too, and the console's input and output have their own).
    /// </summary>
    private static readonly string[] DeviceNames =
    [
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    ];

    /// <summary>
    /// Whether a file name (with or without an extension) is one of Windows' device names (CON, NUL, CONIN$, COM1…,
    /// COM¹…), which no file may have there.
    /// </summary>
    public static bool IsDeviceName(string name) => DeviceNames.Contains(name.Split('.')[0], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Writes the text (UTF-8) through a temporary file of its own next to it (path.&lt;random&gt;.tmp, so two writers of
    /// the same file never take each other's), flushed to disk before the move; makes the folder if needed. The
    /// temporary file is deleted when anything fails.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">Its new contents.</param>
    /// <param name="mode">The Unix permissions it is created with; ignored on Windows.</param>
    /// <param name="overwrite">Whether a file there already is replaced. When not, it is left as it is and the write
    /// fails (IOException): the move is what tells, so a file put there meanwhile is never replaced.</param>
    public static void WriteAllText(string path, string text, UnixFileMode? mode = null, bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = $"{path}.{Guid.NewGuid():N}.tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (mode is { } m && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = m;
        }
        try
        {
            using (var stream = new FileStream(temp, options))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(text);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite);
        }
        catch
        {
            // Not written, or refused (a file put there meanwhile, locked, or read-only on Windows): the temporary
            // copy, which may hold a saved password, is not left behind; the failure goes on to the caller.
            File.Delete(temp);
            throw;
        }
    }
}
