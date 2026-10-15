using System.Globalization;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.App;

/// <summary>
/// The world's portals and which of them are linked, for the map: every portal (the game's wooden and stone ones,
/// and a mod's, by name), its tag, and the pairs the game joins. A pair the save already holds joined (the two ends
/// share a connection) stays a pair while their tags still agree; the rest are paired by tag, as the game does when
/// it next looks (two portals of one tag link; a third of that tag stays alone; portals without a tag link together).
/// </summary>
public static class PortalMap
{
    /// <summary>One portal.</summary>
    /// <param name="Id">The object's id (negative for one added in this session).</param>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="X">World x (metres).</param>
    /// <param name="Y">Height (metres).</param>
    /// <param name="Z">World z (metres).</param>
    /// <param name="Tag">Its tag ("" for none).</param>
    /// <param name="Link">The connection it holds in the save (the other end holds the same), or null for none.</param>
    public sealed record Portal(int Id, string Name, float X, float Y, float Z, string Tag, int? Link);

    /// <summary>The map's portals.</summary>
    /// <param name="Portals">Every portal, east to west then south to north.</param>
    /// <param name="Links">The linked pairs, as indices into <paramref name="Portals"/> (A before B).</param>
    public sealed record Result(List<Portal> Portals, List<(int A, int B)> Links)
    {
        /// <summary>Whether the portal at <paramref name="index"/> has a link.</summary>
        public bool IsLinked(int index) => Links.Any(l => l.A == index || l.B == index);
    }

    /// <summary>The string key holding a portal's tag.</summary>
    private static readonly int TagKey = StableHash.Of("tag");

    /// <summary>The connection type bit of a portal's connection (the other bits: whether it is the target end).</summary>
    private const byte PortalConnection = 0x01;

    /// <summary>
    /// Whether a prefab is a portal: its name says so (portal_wood, portal_stone, a mod's portals), the game's own
    /// bookkeeping objects left out.
    /// </summary>
    public static bool IsPortal(int prefab) => (PrefabCatalog.NameOf(prefab) ?? PrefabCatalog.DisplayName(prefab)) is { } n
        && !n.StartsWith('_') && n.Contains("portal", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the world's portals and those added in this session, deleted ones left out, and pairs them. (Live:
    /// under the world's lock, so what the game changes comes in before or after, not during.)
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="edits">The session's edits (added and deleted objects).</param>
    public static Result Find(WorldSave world, EditStore edits)
    {
        lock (world.Sync)
        {
            return FindIn(world, edits);
        }
    }

    /// <summary><see cref="Find"/>'s work, with the world's lock held.</summary>
    private static Result FindIn(WorldSave world, EditStore edits)
    {
        HashSet<int> deleted = edits.Deleted;
        Dictionary<ChunkFile, byte[]> files = [];
        byte[] Source(ObjectRef o) => world.IsLive ? world.LiveSource(o.File) : (files.TryGetValue(o.File, out byte[]? f) ? f : files[o.File] = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName)));
        IEnumerable<(int Id, Func<byte[]> Bytes)> Candidates()
        {
            for (int id = 0; id < world.ObjectRefs.Count; id++)
            {
                ObjectRef o = world.ObjectRefs[id];
                if (!deleted.Contains(id) && !world.Vanished.Contains(id) && !o.IsTerrain && IsPortal(o.Prefab))
                {
                    yield return (id, () => Source(o)[(int)o.Start..(int)o.End]);
                }
            }
            foreach (NewObject n in edits.Added)
            {
                if (IsPortal(n.Prefab))
                {
                    yield return (n.Id, () => world.NewObjectBytes(n, Source) ?? []);
                }
            }
        }
        List<Portal> portals = [];
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
            string name = PrefabCatalog.NameOf(z.Prefab) ?? PrefabCatalog.DisplayName(z.Prefab) ?? z.Prefab.ToString(CultureInfo.InvariantCulture);
            string tag = z.StringList.FirstOrDefault(s => s.Key == TagKey).Value ?? "";
            int? link = z.Connection is { Length: 5 } c && (c[0] & PortalConnection) != 0 ? BitConverter.ToInt32(c, 1) : null;
            portals.Add(new Portal(id, name, z.Position.X, z.Position.Y, z.Position.Z, tag, link));
        }
        portals.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
        return new Result(portals, Pair(portals));
    }

    /// <summary>
    /// Pairs the portals: first the pairs the save holds joined (two portals sharing a connection, with the same tag),
    /// then the rest by tag in order, two by two.
    /// </summary>
    /// <param name="portals">The portals, in the result's order.</param>
    internal static List<(int A, int B)> Pair(IReadOnlyList<Portal> portals)
    {
        List<(int A, int B)> links = [];
        bool[] free = new bool[portals.Count];
        Array.Fill(free, true);
        foreach (var g in Enumerable.Range(0, portals.Count).Where(i => portals[i].Link != null).GroupBy(i => (portals[i].Link!.Value, portals[i].Tag)))
        {
            var ends = g.ToList();
            if (ends.Count == 2)
            {
                links.Add((ends[0], ends[1]));
                free[ends[0]] = free[ends[1]] = false;
            }
        }
        foreach (var g in Enumerable.Range(0, portals.Count).Where(i => free[i]).GroupBy(i => portals[i].Tag, StringComparer.Ordinal))
        {
            var ends = g.ToList();
            for (int i = 0; i + 1 < ends.Count; i += 2)
            {
                links.Add((ends[i], ends[i + 1]));
            }
        }
        links.Sort();
        return links;
    }
}
