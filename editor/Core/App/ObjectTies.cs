using System.Globalization;
using TerrainEditor.Save;

namespace TerrainEditor.App;

/// <summary>
/// How a mod ties objects to one that names them (a train and the wagons it pulls, a deck and what is built on it), as
/// its worldeditor.json tells it (ModManifests): the root holds the ids of its members (one, or a list in a byte
/// array), each member holds its own. The members stand where the game saves them; the editor moves, turns, copies and
/// deletes them with their root, and gives a pasted copy ids of its own. Every key is a data key, as the mod's code
/// names it.
/// </summary>
/// <param name="Name">What one such group is called in messages ("train"; <see cref="LinkRule.DefaultName"/> when not told).</param>
/// <param name="RootKey">Data key of the ids a root names: an int, a long, or a byte array of ids.</param>
/// <param name="MemberKey">Data key of a member's own id: an int, a long, or a byte array.</param>
/// <param name="IdBytes">How many bytes one id of a byte array is (0: the whole value is one id).</param>
public sealed record TieRule(string Name, int RootKey, int MemberKey, int IdBytes);

/// <summary>
/// Objects tied to a root that names them (TieRule): reading the ids, and the groups they make.
/// </summary>
public static partial class ObjectTies
{
    /// <summary>The rules in use: the mods' manifests' (ModManifests).</summary>
    public static IReadOnlyList<TieRule> Current => ModManifests.Ties;

    /// <summary>The ids a root names under a rule (none when it holds no such value).</summary>
    /// <param name="z">The object's data.</param>
    /// <param name="rule">The rule.</param>
    public static List<byte[]> RootIds(ZdoData z, TieRule rule)
    {
        var ids = new List<byte[]>();
        if (z.GetInt(rule.RootKey) is int i)
        {
            ids.Add(BitConverter.GetBytes(i));
        }
        if (z.GetLong(rule.RootKey) is long l)
        {
            ids.Add(BitConverter.GetBytes(l));
        }
        if (z.GetBytes(rule.RootKey) is { Length: > 0 } bytes)
        {
            ids.AddRange(Split(bytes, rule.IdBytes));
        }
        return ids;
    }

    /// <summary>A member's own id under a rule; null when it holds none.</summary>
    /// <param name="z">The object's data.</param>
    /// <param name="rule">The rule.</param>
    public static byte[]? MemberId(ZdoData z, TieRule rule)
    {
        if (z.GetInt(rule.MemberKey) is int i)
        {
            return BitConverter.GetBytes(i);
        }
        if (z.GetLong(rule.MemberKey) is long l)
        {
            return BitConverter.GetBytes(l);
        }
        return z.GetBytes(rule.MemberKey) is { Length: > 0 } bytes ? bytes : null;
    }

    /// <summary>
    /// The groups among objects: each root with the members naming one of its ids (a member a root names once, the first
    /// root reading it; a root naming none of them makes no group). An object is in one group at most.
    /// </summary>
    /// <param name="objects">The objects, by a number of the caller's (an index), with their data.</param>
    /// <param name="rules">The rules.</param>
    /// <returns>Each group, its root first, then its members in the root's order, with its rule.</returns>
    public static List<(List<int> Group, TieRule Rule)> Groups(IReadOnlyList<(int Index, ZdoData Data)> objects, IReadOnlyList<TieRule> rules)
    {
        var groups = new List<(List<int>, TieRule)>();
        var taken = new HashSet<int>();
        foreach (var rule in rules)
        {
            var members = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (index, data) in objects)
            {
                if (MemberId(data, rule) is { } id)
                {
                    members.TryAdd(Convert.ToBase64String(id), index);
                }
            }
            foreach (var (index, data) in objects)
            {
                if (taken.Contains(index))
                {
                    continue;
                }
                var group = new List<int> { index };
                foreach (byte[] id in RootIds(data, rule))
                {
                    if (members.TryGetValue(Convert.ToBase64String(id), out int m) && m != index && !taken.Contains(m) && !group.Contains(m))
                    {
                        group.Add(m);
                    }
                }
                if (group.Count > 1)
                {
                    taken.UnionWith(group);
                    groups.Add((group, rule));
                }
            }
        }
        return groups;
    }

    /// <summary>Whether data holds a key any rule reads.</summary>
    public static bool HoldsAny(ZdoData z, IReadOnlyList<TieRule> rules) =>
        rules.Any(r => RootIds(z, r).Count > 0 || MemberId(z, r) != null);

    /// <summary>A byte array cut into ids of <paramref name="size"/> bytes (0: one id); a part left over is no id.</summary>
    internal static IEnumerable<byte[]> Split(byte[] bytes, int size)
    {
        if (size <= 0)
        {
            yield return bytes;
            yield break;
        }
        for (int i = 0; i + size <= bytes.Length; i += size)
        {
            yield return bytes[i..(i + size)];
        }
    }

    /// <summary>A number as the data text of its section.</summary>
    internal static string Text(long v) => v.ToString(CultureInfo.InvariantCulture);
}
