namespace TerrainEditor.App;

/// <summary>
/// Reading a blueprint file (PlanBuild .blueprint or .vbuild) into its pieces, terrain entries and headers.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// Reads a PlanBuild .blueprint or a .vbuild file (chosen by the file name's extension). Lines that cannot be read
    /// are counted, not fatal; other mods' sections are skipped. The editor's #EditorData lines are attached to their
    /// pieces, and a ruin's pieces get the no-builder field.
    /// </summary>
    /// <param name="fileName">The file's name or path: gives the format and the fallback name.</param>
    /// <param name="text">The file's content.</param>
    public static Parsed Parse(string fileName, string text)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        bool vbuild = ext == ".vbuild";
        string name = Path.GetFileNameWithoutExtension(fileName);
        string? creator = null, description = null;
        List<Piece> pieces = [];
        List<TerrainMod> terrain = [];
        int skipped = 0, line0 = 0;
        bool ruin = false;
        var data = new Dictionary<int, List<Editing.ObjectField>>();
        var lines = new List<int>();
        string section = "pieces";
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }
            if (HeaderValue(line, "#Name:") is { } named) { name = named.Length > 0 ? named : name; continue; }
            if (HeaderValue(line, "#Creator:") is { } by) { creator = by; continue; }
            if (HeaderValue(line, "#Description:") is { } about) { description = Unquote(about); continue; }
            if (line.StartsWith("#Category:", StringComparison.Ordinal)) { continue; }
            if (line == RuinHeader) { ruin = true; continue; }
            if (line.StartsWith(DataHeader, StringComparison.Ordinal))
            {
                ReadDataLine(line, data);
                continue;
            }
            if (line.StartsWith('#'))
            {
                section = SectionAfter(line);
                continue;
            }
            try
            {
                switch (section)
                {
                    case "terrain":
                        terrain.Add(ParseTerrain(line));
                        break;
                    case "pieces":
                        int n = line0++;
                        pieces.Add(vbuild ? ParseVBuild(line) : ParseBlueprintPiece(line));
                        lines.Add(n);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException)
            {
                // Said for the first only: a broken file would fill the log; the count is kept.
                if (skipped++ == 0)
                {
                    Log.CaughtBrief(ex, "reading a blueprint line (it and any other unreadable lines are skipped)");
                }
            }
        }
        AttachData(pieces, lines, data, ruin);
        return new Parsed(name, creator, description, pieces, terrain, skipped, ruin);
    }
}
