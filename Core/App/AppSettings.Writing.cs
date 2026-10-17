using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Writing <see cref="AppSettings"/> to its settings file.
/// </summary>
public sealed partial class AppSettings
{
    /// <summary>
    /// JSON options for writing the settings file, indented so it stays readable by hand.
    /// </summary>
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// A file that cannot be written (locked, read-only) is left as it was: the settings stay in memory. So is one
    /// that could not be read when these were loaded (<c>FileUnread</c>), or that was not there then and is now
    /// (another program put it back): the user's settings are in it.
    /// </summary>
    /// <returns>Null when saved, else why not (also logged).</returns>
    public string? Save()
    {
        lock (Lock)
        {
            string? problem = null;
            if (FileUnread)
            {
                // Not its path: finding it may be what failed then (the data folder could not be made).
                problem = "settings.json could not be read when the editor started, and is left as it is.";
                Console.WriteLine($"Settings: not saved: {problem}");
            }
            else
            {
                // Found once, as in Load: the logs of a failure to find it must not look for it again.
                string? path = null;
                try
                {
                    path = FilePath;
                    // Only the user can read it, from the start: it holds Claude's token. Not over a file that was not
                    // there at the start: the move itself refuses one put there since.
                    SafeFile.WriteAllText(path, JsonSerializer.Serialize(this, Indented), UnixFileMode.UserRead | UnixFileMode.UserWrite, overwrite: !_fileMissing);
                    _fileMissing = false;
                }
                catch (IOException ex) when (_fileMissing && path != null && File.Exists(path))
                {
                    Log.Caught(ex, "saving settings.json (not there at the start, now put back by another program: left as it is)");
                    problem = $"{path} was not there when the editor started and is now (put back by another program?), and is left as it is.";
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Caught(ex, "saving settings.json (the settings stay in memory)");
                    problem = $"could not write {path ?? "settings.json"}: {ex.Message}";
                }
            }
            return problem;
        }
    }
}
