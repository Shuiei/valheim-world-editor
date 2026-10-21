using System.Text.Json;

namespace TerrainEditor.App;

/// <summary>
/// Loading a language's catalogue: the carried file and the user's file, each checked, and their merge.
/// </summary>
public static partial class Lang
{
    /// <summary>
    /// The catalogue of <paramref name="code"/>: the carried one with the user's file over it, each one's texts checked
    /// against the English first (those of no English key, or that do not take the English one's values, are left out,
    /// so a broken correction leaves the carried text); null when there is none.
    /// </summary>
    internal static Dictionary<string, string>? Load(string code)
    {
        var carried = Carried(code);
        var user = UserFile(code);
        if (carried == null && user == null)
        {
            return null;
        }
        static Dictionary<string, string> Checked(Dictionary<string, string>? texts) =>
            (texts ?? []).Where(kv => English.TryGetValue(kv.Key, out string? en) && Fits(en, kv.Value)).ToDictionary();
        return Merge(Checked(carried), Checked(user));
    }
    /// <summary>A catalogue carried inside the app, null when there is none.</summary>
    private static Dictionary<string, string>? Carried(string code)
    {
        using var s = typeof(Lang).Assembly.GetManifestResourceStream($"TerrainEditor.Lang.{code}.json");
        return s == null ? null : Parse(s);
    }

    /// <summary>The user's catalogue of <paramref name="code"/> in <c>Folder</c>; null when there is none or it cannot be read (logged).</summary>
    private static Dictionary<string, string>? UserFile(string code)
    {
        string path = Path.Combine(Folder, code + ".json");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            using var s = File.OpenRead(path);
            return Parse(s);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Caught(e, $"reading the language catalogue {Path.GetFileName(path)} (English is shown for it)");
            return null;
        }
    }

    /// <summary>
    /// A catalogue's file: one JSON object of texts by key (a value that is not a string is skipped; of a key given
    /// twice the last text counts).
    /// </summary>
    /// <exception cref="JsonException">The file is not JSON, or its root is not an object.</exception>
    internal static Dictionary<string, string> Parse(Stream json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"a language file is one object of texts by key, not {doc.RootElement.ValueKind}");
        }
        var texts = new Dictionary<string, string>();
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Value.ValueKind == JsonValueKind.String)
            {
                texts[p.Name] = p.Value.GetString()!;
            }
        }
        return texts;
    }

    /// <summary><paramref name="over"/>'s texts over a copy of <paramref name="under"/>'s.</summary>
    private static Dictionary<string, string> Merge(IReadOnlyDictionary<string, string> under, Dictionary<string, string>? over)
    {
        var texts = new Dictionary<string, string>(under);
        foreach (var (k, v) in over ?? [])
        {
            texts[k] = v;
        }
        return texts;
    }
}
