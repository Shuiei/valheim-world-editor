namespace TerrainEditor.App;

/// <summary>
/// Reading a blueprint's header lines (name, creator, description, tags, world and the piece counts), and changing
/// them again in its text.
/// </summary>
public static partial class Homestead
{
    /// <summary>A blueprint from its lines, without its picture or time yet.</summary>
    /// <param name="file">Its path, for the entry's name when it has no #Name:.</param>
    /// <param name="lines">The file's lines.</param>
    public static Entry Read(string file, IEnumerable<string> lines)
    {
        string? name = null, creator = null, world = null, description = null, tags = null;
        int pieces = 0;
        var kinds = new Dictionary<string, int>();
        bool inPieces = false;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (BlueprintFormats.HeaderValue(line, "#Name:") is { } named)
            {
                name = named;
            }
            else if (BlueprintFormats.HeaderValue(line, "#Creator:") is { } by)
            {
                creator = by;
            }
            else if (BlueprintFormats.HeaderValue(line, "#Description:") is { } about)
            {
                // As a paste reads it: a quoted description loses its quotes, one only starting with a quote keeps it.
                description = BlueprintFormats.Unquote(about);
            }
            else if (line.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = line[6..];
            }
            else if (line.StartsWith("#HomesteadWorld:", StringComparison.OrdinalIgnoreCase))
            {
                world = line[16..].Trim();
            }
            else if (line.StartsWith('#'))
            {
                inPieces = line.Equals("#Pieces", StringComparison.OrdinalIgnoreCase);
            }
            else if (inPieces && line.Length > 0)
            {
                pieces++;
                string kind = line.Split(';')[0].Trim();
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            }
        }
        return new Entry(file, string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file) : name, creator,
            string.IsNullOrWhiteSpace(world) ? null : world, pieces, null, default)
        {
            Description = description ?? "",
            Tags = Details.ParseTags(tags),
            Kinds = kinds,
        };
    }

    /// <summary>
    /// A blueprint's name, description and tags changed in its file (the pieces stay as they are). The name
    /// goes first, the description and tags after #Creator: (else after the name).
    /// </summary>
    /// <param name="text">The blueprint file's text.</param>
    /// <param name="details">What to write; no #Tags: line when it has no tags.</param>
    /// <returns>The new text, with \n line ends.</returns>
    public static string WithDetails(string text, Details details)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        int pieces = lines.FindIndex(l => l.Trim().Equals("#Pieces", StringComparison.OrdinalIgnoreCase));
        if (pieces < 0)
        {
            pieces = lines.Count;
        }
        // Matched as Read matches them, spaces before the # aside (a hand-edited " #Name: Old" stayed, and won).
        var head = lines.Take(pieces).Where(l => !(l.TrimStart().StartsWith("#Name:", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("#Description:", StringComparison.OrdinalIgnoreCase)
            || l.TrimStart().StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))).ToList();
        var mine = new List<string> { "#Name:" + Header(details.Name), "#Description:" + Header(details.Description) };
        if (details.Tags.Count > 0)
        {
            mine.Add("#Tags:" + Header(string.Join(", ", details.Tags)));
        }
        int creator = head.FindIndex(l => l.TrimStart().StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase));
        head.InsertRange(0, mine.Take(1));
        head.InsertRange(creator < 0 ? 1 : creator + 2, mine.Skip(1));
        return string.Join("\n", head.Concat(lines.Skip(pieces)));
    }
}
