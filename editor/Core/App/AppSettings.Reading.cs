using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Reading <see cref="AppSettings"/> from its settings file.
/// </summary>
public sealed partial class AppSettings
{
    /// <summary>
    /// Reads the settings file. A missing file gives the defaults; one that is not valid JSON is first copied aside as
    /// settings.json.bad; one that cannot be read gives the defaults with <c>FileUnread</c> set, which are not saved
    /// over it.
    /// </summary>
    public static AppSettings Load()
    {
        lock (Lock)
        {
            // The path is found once: finding it can fail too (the data folder cannot be made).
            string path;
            string? text;
            try
            {
                path = FilePath;
                text = ReadText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Caught(ex, "reading settings.json (the defaults are used for now, and it is not saved over)");
                return new AppSettings { FileUnread = true };
            }
            if (text == null)
            {
                return new AppSettings { _fileMissing = true };
            }
            try
            {
                return (JsonSerializer.Deserialize<AppSettings>(text) ?? new AppSettings()).WithoutNulls();
            }
            catch (JsonException ex)
            {
                // Not readable: kept aside as settings.json.bad (the next save would write over it).
                Log.Caught(ex, "reading settings.json (kept as settings.json.bad, starting from the defaults)");
                try
                {
                    File.Copy(path, path + ".bad", overwrite: true);
                }
                catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
                {
                    Log.Caught(copy, "keeping the unreadable settings.json as settings.json.bad");
                }
                return new AppSettings();
            }
        }
    }

    /// <summary>
    /// The settings file's text, or null when there is none. A read that fails (the file held by a sync or antivirus
    /// program for a moment: IOException, or access denied while it is) is tried again twice, 100 ms apart, before
    /// its exception goes up; a file not there is looked for once more (one being put back by such a program), and
    /// none the second time is none (no data folder: none at once).
    /// </summary>
    /// <param name="path">The settings file.</param>
    private static string? ReadText(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (DirectoryNotFoundException e)
            {
                Log.CaughtBrief(e, "reading settings.json (none yet: the defaults)");
                return null;
            }
            catch (FileNotFoundException e) when (attempt > 0)
            {
                Log.CaughtBrief(e, "reading settings.json (none yet: the defaults)");
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 2)
            {
                Log.CaughtBrief(ex, "reading settings.json (tried again in 100 ms)");
                Thread.Sleep(100);
            }
        }
    }
}
