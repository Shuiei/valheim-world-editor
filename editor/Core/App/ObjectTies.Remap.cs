using System.Globalization;
using TerrainEditor.Editing;

namespace TerrainEditor.App;

/// <summary>
/// A pasted copy's ties made its own: each id a copied root names gets a new one, random, in the root and in the copied
/// members naming it, so the copy is a group of its own and never takes the original's members.
/// </summary>
public static partial class ObjectTies
{
    /// <summary>The data sections a tie's ids are kept in.</summary>
    private static readonly string[] IdSections = ["ints", "longs", "bytes"];

    /// <summary>Whether a copy's objects hold any key a rule reads (as their data fields say).</summary>
    public static bool AnyIn(IReadOnlyList<NewObject> copy, IReadOnlyList<TieRule> rules) =>
        rules.Count > 0 && copy.Any(o => o.Data?.Any(f => f.Value != null && rules.Any(r => f.Key == r.RootKey || f.Key == r.MemberKey)) == true);

    /// <summary>
    /// One pasted copy's ties made its own (its objects as their data fields hold them): every id a root of the copy
    /// names whose member is in the copy gets a new random id, in the root and in that member; a root's single id (an int
    /// or a long) is always new; a list keeps only the ids of members copied; a member whose root was not copied is no
    /// member any more (its id goes). Objects without ties come back as they were.
    /// </summary>
    /// <param name="copy">The copy's objects, each with all its data.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="random">Draws the new ids.</param>
    public static NewObject[] Remap(IReadOnlyList<NewObject> copy, IReadOnlyList<TieRule> rules, Random random)
    {
        var data = copy.Select(o => o.Data?.ToList()).ToArray();
        foreach (var rule in rules)
        {
            RemapRule(data, rule, random);
        }
        return [.. copy.Select((o, i) => data[i] is { } d && o.Data != null && !d.SequenceEqual(o.Data) ? o with { Data = d } : o)];
    }

    /// <summary>One rule's ids made the copy's own, in place.</summary>
    private static void RemapRule(List<ObjectField>?[] data, TieRule rule, Random random)
    {
        // Each member's id, by its value.
        var members = new Dictionary<string, List<(int Object, int Field)>>(StringComparer.Ordinal);
        for (int i = 0; i < data.Length; i++)
        {
            if (Field(data[i], rule.MemberKey) is var (f, id))
            {
                string k = Convert.ToBase64String(id);
                (members.TryGetValue(k, out var list) ? list : members[k] = []).Add((i, f));
            }
        }
        var made = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        byte[] New(byte[] old)
        {
            string k = Convert.ToBase64String(old);
            if (!made.TryGetValue(k, out byte[]? id))
            {
                id = new byte[old.Length];
                do
                {
                    random.NextBytes(id);
                }
                while (id.All(b => b == 0));
                made[k] = id;
            }
            return id;
        }
        for (int i = 0; i < data.Length; i++)
        {
            if (Field(data[i], rule.RootKey) is not var (f, value))
            {
                continue;
            }
            var field = data[i]![f];
            if (field.Section == "bytes")
            {
                byte[] kept = [.. Split(value, rule.IdBytes).Where(id => members.ContainsKey(Convert.ToBase64String(id))).SelectMany(New)];
                data[i]![f] = field with { Value = Convert.ToBase64String(kept) };
            }
            else
            {
                data[i]![f] = field with { Value = Number(field.Section, New(value)) };
            }
        }
        foreach (var (k, list) in members)
        {
            byte[]? id = made.GetValueOrDefault(k);
            foreach (var (i, f) in list)
            {
                var field = data[i]![f];
                data[i]![f] = field with { Value = id == null ? null : field.Section == "bytes" ? Convert.ToBase64String(id) : Number(field.Section, id) };
            }
        }
    }

    /// <summary>The field of a key in an id section, and its value's bytes; null when there is none (or it cannot be read).</summary>
    private static (int Field, byte[] Value)? Field(List<ObjectField>? data, int key)
    {
        if (data == null)
        {
            return null;
        }
        int f = data.FindIndex(x => x.Key == key && x.Value != null && IdSections.Contains(x.Section));
        if (f < 0)
        {
            return null;
        }
        string v = data[f].Value!;
        return data[f].Section switch
        {
            "ints" when int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) => (f, BitConverter.GetBytes(n)),
            "longs" when long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) => (f, BitConverter.GetBytes(n)),
            "bytes" => Bytes(v) is { Length: > 0 } b ? (f, b) : null,
            _ => null,
        };
    }

    /// <summary>Base64 text as bytes; null when it is not base64.</summary>
    private static byte[]? Bytes(string text)
    {
        byte[] buffer = new byte[text.Length];
        return Convert.TryFromBase64String(text, buffer, out int n) ? buffer[..n] : null;
    }

    /// <summary>An id as a number field's text: an int of four bytes, a long of eight; null (no value) for another size.</summary>
    private static string? Number(string section, byte[] id) => (section, id.Length) switch
    {
        ("ints", 4) => Text(BitConverter.ToInt32(id)),
        ("longs", 8) => Text(BitConverter.ToInt64(id)),
        _ => null,
    };
}
