namespace TerrainEditor.App;

/// <summary>
/// Reading a blueprint's header lines (name, creator, description, tags, world, the editor's marks and the piece
/// counts), and changing them again in its text.
/// </summary>
public static partial class Homestead
{
    /// <summary>A blueprint from its lines, without its picture or time yet.</summary>
    /// <param name="file">Its path, for the entry's name when it has no #Name:.</param>
    /// <param name="lines">The file's lines.</param>
    public static Entry Read(string file, IEnumerable<string> lines)
    {
        var head = new Headers();
        int pieces = 0, marked = 0;
        var kinds = new Dictionary<string, int>();
        bool inPieces = false;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (head.Take(line))
            {
                continue;
            }
            if (line.StartsWith('#'))
            {
                inPieces = line.Equals("#Pieces", StringComparison.OrdinalIgnoreCase);
            }
            else if (inPieces && line.Length > 0)
            {
                // A marked piece (the hammer skips it) is counted, not costed; marks in a file without the header are not its,
                // and a mark with no piece at its line (a file cut short or edited by hand) marks nothing.
                int index = pieces++;
                if (head.HasMarks && head.Marked.Contains(index))
                {
                    marked++;
                }
                else
                {
                    string kind = line.Split(';')[0].Trim();
                    kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
                }
            }
        }
        return new Entry(file, string.IsNullOrWhiteSpace(head.Name) ? Path.GetFileNameWithoutExtension(file) : head.Name, head.Creator,
            string.IsNullOrWhiteSpace(head.World) ? null : head.World, pieces, null, default)
        {
            Description = head.Description ?? "",
            Tags = Details.ParseTags(head.Tags),
            Kinds = kinds,
            EditorOnly = head.HasMarks ? marked : null,
        };
    }

    /// <summary>The header lines of a blueprint as <see cref="Read(string, IEnumerable{string})"/> gathers them, one at a time.</summary>
    private sealed class Headers
    {
        /// <summary>The #Name: value; null when there is none.</summary>
        public string? Name { get; private set; }
        /// <summary>The #Creator: value; null when there is none.</summary>
        public string? Creator { get; private set; }
        /// <summary>The #HomesteadWorld: value; null when there is none.</summary>
        public string? World { get; private set; }
        /// <summary>The #Description: value, unquoted; null when there is none.</summary>
        public string? Description { get; private set; }
        /// <summary>The #Tags: value, as written; null when there is none.</summary>
        public string? Tags { get; private set; }
        /// <summary>Whether the file carries the editor's marks (<c>BlueprintFormats.MarkedHeader</c>).</summary>
        public bool HasMarks { get; private set; }
        /// <summary>The lines (among the pieces, from 0) of the pieces marked as the editor's alone.</summary>
        public HashSet<int> Marked { get; } = [];

        /// <summary>Takes a header line this gathers; false for any other line.</summary>
        /// <param name="line">The line, trimmed.</param>
        public bool Take(string line)
        {
            if (BlueprintFormats.HeaderValue(line, "#Name:") is { } named)
            {
                Name = named;
            }
            else if (BlueprintFormats.HeaderValue(line, "#Creator:") is { } by)
            {
                Creator = by;
            }
            else if (BlueprintFormats.HeaderValue(line, "#Description:") is { } about)
            {
                // As a paste reads it: a quoted description loses its quotes, one only starting with a quote keeps it.
                Description = BlueprintFormats.Unquote(about);
            }
            else if (line.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))
            {
                Tags = line[6..];
            }
            else if (line.StartsWith("#HomesteadWorld:", StringComparison.OrdinalIgnoreCase))
            {
                World = line[16..].Trim();
            }
            else if (line == BlueprintFormats.MarkedHeader)
            {
                HasMarks = true;
            }
            else if (BlueprintFormats.MarkedLine(line) is int at)
            {
                Marked.Add(at);
            }
            else
            {
                return false;
            }
            return true;
        }
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
