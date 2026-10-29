using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Reading place.json: a first run starts empty, a broken file is kept aside, and nulls are taken as empty.
public sealed partial class PlaceMemory
{
    /// <summary>
    /// Reads the file. A missing one gives an empty memory; a broken one is first copied aside as place.json.bad (the
    /// next save writes over it), then gives an empty memory; one that cannot be read (or kept aside) gives an empty
    /// memory that is never saved over it. Nulls a hand-edited file holds are taken as empty, and presets without a
    /// name or kinds are left out.
    /// </summary>
    public static PlaceMemory Load()
    {
        string text;
        try
        {
            // Read at once: File.Exists says no for a file there that cannot be read, which must not count as none.
            text = File.ReadAllText(Where);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // None yet (the first run): brief.
            Log.CaughtBrief(ex, "reading place.json (none yet: starting empty)");
            return new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading place.json (starting empty, not saving over it)");
            return new() { _unread = true };
        }
        try
        {
            return (JsonSerializer.Deserialize<PlaceMemory>(text) ?? new()).Mended();
        }
        catch (JsonException ex)
        {
            Log.Caught(ex, "parsing place.json (kept as place.json.bad, starting empty)");
            try
            {
                File.Copy(Where, Where + ".bad", overwrite: true);
                return new();
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Log.Caught(copy, "keeping the unreadable place.json as place.json.bad (not saving over it)");
                return new() { _unread = true };
            }
        }
    }

    /// <summary>This memory with the nulls of a hand-edited file taken as empty, and its presets without a name or kinds left out.</summary>
    private PlaceMemory Mended()
    {
        // The JSON may hold null where the types say there is none.
        Presets = [.. (Presets ?? []).Where(p => p?.Name != null && p.Kinds != null)];
        Favourites = [.. (Favourites ?? []).Where(n => n != null)];
        Recent = [.. (Recent ?? []).Where(n => n != null)];
        Chosen = [.. (Chosen ?? []).Where(n => n != null)];
        Weights ??= [];
        Builders ??= [];
        return this;
    }
}
