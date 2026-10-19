// BlueprintFormats: a part of the blueprint reader (see BlueprintFormats.cs).

using System.Globalization;

namespace TerrainEditor.App;

/// <summary>
/// The editor's own #EditorData lines of a blueprint file: read onto each piece, which then holds the object's fields.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// Reads an editor data line ("#EditorData:n;section;key;value"): the field is attached to the piece whose line
    /// among the pieces is n. A line that does not read is left out.
    /// </summary>
    /// <param name="line">The whole line.</param>
    /// <param name="data">The fields by piece line, added to.</param>
    internal static void ReadDataLine(string line, Dictionary<int, List<Editing.ObjectField>> data)
    {
        string rest = line[DataHeader.Length..];
        int bar = rest.IndexOf(';');
        if (bar > 0 && int.TryParse(rest[..bar], NumberStyles.Integer, CultureInfo.InvariantCulture, out int at) && Editing.ObjectField.Parse(rest[(bar + 1)..]) is { } f)
        {
            (data.TryGetValue(at, out var l) ? l : data[at] = []).Add(f);
        }
    }

    /// <summary>
    /// The piece line (from 0) an editor data line marks as the editor's alone ("#EditorData:n;editor;only;1"); null
    /// for any other line. <see cref="ReadDataLine"/> leaves such a line out, as its field is no object field.
    /// </summary>
    /// <param name="line">The whole line, trimmed.</param>
    internal static int? MarkedLine(string line)
    {
        if (!line.StartsWith(DataHeader, StringComparison.Ordinal) || !line.EndsWith(";" + OnlyMark, StringComparison.Ordinal))
        {
            return null;
        }
        string number = line[DataHeader.Length..^(OnlyMark.Length + 1)];
        return int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int at) && at >= 0 ? at : null;
    }

    /// <summary>
    /// Gives each piece the #EditorData fields written for it (by its line among the pieces), and the no-builder field
    /// when the file is a ruin. A piece with neither keeps its data as it was.
    /// </summary>
    /// <param name="pieces">The pieces, in file order; changed in place.</param>
    /// <param name="lines">Each piece's line among the pieces, in the same order.</param>
    /// <param name="data">The fields by that line.</param>
    /// <param name="ruin">Whether the file is a ruin.</param>
    internal static void AttachData(List<Piece> pieces, List<int> lines, Dictionary<int, List<Editing.ObjectField>> data, bool ruin)
    {
        if (data.Count > 0 || ruin)
        {
            for (int k = 0; k < pieces.Count; k++)
            {
                var d = data.TryGetValue(lines[k], out var held) ? held : [];
                if (ruin)
                {
                    d.Add(Editing.ObjectField.NoBuilder);
                }
                pieces[k] = pieces[k] with { Data = d };
            }
        }
    }
}
