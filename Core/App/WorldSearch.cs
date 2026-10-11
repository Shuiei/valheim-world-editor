using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// Searching the whole world (like Amulet's find, or WorldEdit's //count): objects of a kind, containers
/// holding an item, or objects whose texts match (signs, portal tags, ward and tombstone names).
/// </summary>
public static class WorldSearch
{
    /// <summary>One object found.</summary>
    /// <param name="Id">The object's id (negative for one added in this session).</param>
    /// <param name="Name">Its display name, else its prefab hash.</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Y">Height (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="Match">What matched ("Item ×stack", "key: text"); null for a kind search.</param>
    public sealed record Hit(int Id, string Name, float X, float Y, float Z, string? Match);

    /// <summary>A search's results.</summary>
    /// <param name="Hits">The hits, at most the limit.</param>
    /// <param name="Counts">
    /// Per kind name, item name (stack sizes added up) or, for texts, object name: how many were found.
    /// </param>
    /// <param name="Total">How many hits there were in all.</param>
    /// <param name="Truncated">Whether more hits were found than <paramref name="Hits"/> holds.</param>
    public sealed record Result(List<Hit> Hits, Dictionary<string, int> Counts, int Total, bool Truncated);

    /// <summary>The byte-array key holding a container's inventory.</summary>
    private static readonly int ItemsKey = StableHash.Of("items");

    /// <summary>
    /// Searches the world's objects and those added in this session, deleted ones left out; matching ignores
    /// case. (Live: under the world's lock, so what the game changes comes in before or after, not during.)
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (added and deleted objects).</param>
    /// <param name="query">The text looked for; empty finds nothing. Names starting with _ only when it does too.</param>
    /// <param name="what">"kinds" (object names), "items" (container contents), anything else: texts.</param>
    /// <param name="limit">How many hits to keep.</param>
    public static Result Search(WorldSave world, EditStore edits, string query, string what, int limit = 2000)
    {
        lock (world.Sync)
        {
            return SearchIn(world, edits, query, what, limit);
        }
    }

    /// <summary><see cref="Search"/>'s work, with the world's lock held.</summary>
    private static Result SearchIn(WorldSave world, EditStore edits, string query, string what, int limit)
    {
        query = query.Trim();
        List<Hit> hits = [];
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        int total = 0;
        if (query.Length == 0)
        {
            return new Result(hits, counts, 0, false);
        }
        HashSet<int> deleted = edits.Deleted;
        // amount: how much the hit adds to its count (items: the stack sizes).
        void Found(int id, string name, System.Numerics.Vector3 p, string? match, string countKey, int amount = 1)
        {
            total++;
            counts[countKey] = counts.GetValueOrDefault(countKey) + amount;
            if (hits.Count < limit)
            {
                hits.Add(new Hit(id, name, p.X, p.Y, p.Z, match));
            }
        }
        bool Matches(string s) => s.Contains(query, StringComparison.OrdinalIgnoreCase);
        // The game's own bookkeeping objects (_ZoneCtrl, _TerrainCompiler...) only when asked for by name.
        bool Shown(string name) => !name.StartsWith('_') || query.StartsWith('_');

        if (what == "kinds")
        {
            for (int id = 0; id < world.ObjectRefs.Count; id++)
            {
                ObjectRef o = world.ObjectRefs[id];
                if (deleted.Contains(id) || world.Vanished.Contains(id) || o.IsTerrain || o.Prefab == WorldSave.LocationProxyPrefab)
                {
                    continue;
                }
                string? name = PrefabCatalog.DisplayName(o.Prefab);
                if (name != null && Shown(name) && Matches(name))
                {
                    Found(id, name, o.Position, null, name);
                }
            }
            foreach (NewObject n in edits.Added)
            {
                string? name = PrefabCatalog.DisplayName(n.Prefab);
                if (name != null && Shown(name) && Matches(name))
                {
                    Found(n.Id, name, n.Position, null, name);
                }
            }
            return new Result(hits, counts, total, total > hits.Count);
        }

        // Items and texts: only objects with byte arrays (containers) or strings are read.
        ushort needed = what == "items" ? ZdoData.ByteArrays : ZdoData.Strings;
        Dictionary<ChunkFile, byte[]> files = [];
        byte[] Source(ObjectRef o) => world.IsLive ? world.LiveSource(o.File) : (files.TryGetValue(o.File, out byte[]? f) ? f : files[o.File] = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)));
        IEnumerable<(int Id, Func<byte[]> Bytes)> Candidates()
        {
            for (int id = 0; id < world.ObjectRefs.Count; id++)
            {
                ObjectRef o = world.ObjectRefs[id];
                if (!deleted.Contains(id) && !world.Vanished.Contains(id) && !o.IsTerrain && (o.Flags & needed) != 0)
                {
                    yield return (id, () => Source(o)[(int)o.Start..(int)o.End]);
                }
            }
            foreach (NewObject n in edits.Added)
            {
                yield return (n.Id, () => world.NewObjectBytes(n, Source) ?? []);
            }
        }
        foreach (var (id, bytes) in Candidates())
        {
            byte[] b = bytes();
            if (b.Length == 0)
            {
                continue;
            }
            ZdoData z;
            try
            {
                z = ZdoData.Parse(b);
            }
            catch (Exception ex) when (ex is EndOfStreamException or IOException)
            {
                continue;
            }
            string name = PrefabCatalog.DisplayName(z.Prefab) ?? z.Prefab.ToString(CultureInfo.InvariantCulture);
            if (what == "items")
            {
                if (z.GetBytes(ItemsKey) is not byte[] items)
                {
                    continue;
                }
                InventoryData inv;
                try
                {
                    inv = InventoryData.Read(items);
                }
                catch (Exception ex) when (ex is NotSupportedException or EndOfStreamException or IOException)
                {
                    continue;
                }
                foreach (var g in inv.Items.GroupBy(i => PrefabCatalog.NameOf(i.Prefab) ?? "?").Where(g => Matches(g.Key)))
                {
                    int stack = g.Sum(i => i.Stack);
                    Found(id, name, z.Position, $"{g.Key} ×{stack}", g.Key, stack);
                }
            }
            else
            {
                foreach (var (key, value) in z.StringList)
                {
                    if (value.Length > 0 && Matches(value))
                    {
                        Found(id, name, z.Position, $"{ZdoKeys.NameOf(key) ?? "text"}: {value}", name);
                        break;
                    }
                }
            }
        }
        return new Result(hits, counts, total, total > hits.Count);
    }
}
