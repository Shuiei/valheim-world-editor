using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

public static partial class PrefabCatalog
{
    /// <summary>
    /// Takes the prefabs by name, as GameCatalogues' PrefabScan writes them.
    /// </summary>
    public static void Use(string json)
    {
        var all = JsonSerializer.Deserialize<Dictionary<string, Entry>>(json)!;
        var d = new Data([], [], [], [], [], [], [], [], [], [], []);
        foreach (var (name, e) in all)
        {
            int hash = StableHash.Of(name);
            d.AllNames[hash] = name;
            if (e.c != 0)
            {
                d.Creatures.Add(hash);
            }
            if (e.i != 0)
            {
                d.ItemNames.Add(name);
            }
            AddItemRules(d, hash, e);
            AddSpawn(d, hash, e);
            // Save-file flags: persistent 0x100, distant 0x200, object type in bits 10-11.
            ushort flags = (ushort)((e.p != 0 ? 0x100 : 0) | (e.d != 0 ? 0x200 : 0) | ((e.t & 3) << 10));
            Info info = new(name, flags, (float)e.gr, e.cult != 0, e.cw, e.ch, (float)e.ward, (float)e.build);
            AddGrownFrom(d, name, e);
            if (e.p == 0 || e.c != 0 || e.i != 0 || name.EndsWith("_ragdoll", StringComparison.Ordinal))
            {
                d.Extra[hash] = info;
                continue;
            }
            d.ByHash[hash] = info;
        }
        d.ItemNames.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (var p in d.ByHash.Values.Where(p => p.GrowRadius > 0))
        {
            d.Grow[p.Name] = (p.GrowRadius, p.NeedsCultivated);
        }
        foreach (var (name, g) in d.GrownFrom)
        {
            d.Grow.TryAdd(name, (g.Radius, false));
        }
        _catalogue = d;
    }

    /// <summary>
    /// One prefab as PrefabScan writes it (flags as 0 or 1).
    /// </summary>
    /// <param name="p">Persistent.</param>
    /// <param name="d">Distant.</param>
    /// <param name="t">Object type (two bits).</param>
    /// <param name="c">A creature.</param>
    /// <param name="i">An item.</param>
    /// <param name="gr">Grow radius, metres.</param>
    /// <param name="cult">Grows only on cultivated ground.</param>
    /// <param name="cw">Container grid columns.</param>
    /// <param name="ch">Container grid rows.</param>
    /// <param name="ward">Ward radius.</param>
    /// <param name="build">Crafting station build range.</param>
    /// <param name="grows">What a sapling grows into.</param>
    /// <param name="it">Item type; -1 for anything that is not an item.</param>
    /// <param name="ao">Attach override type.</param>
    /// <param name="var">Number of variants.</param>
    /// <param name="at">Has an "attach" child.</param>
    /// <param name="ats">Has an "attach_skin" child.</param>
    /// <param name="dur">Durability when new at quality 1.</param>
    /// <param name="durl">Durability added per quality level.</param>
    /// <param name="ms">Item: the largest stack (absent: 1, it does not stack). The catalogue is scanned again by each
    /// build of the editor (GameData's cache stamp), so none lacks it.</param>
    /// <param name="is">Item stand: item types it takes.</param>
    /// <param name="isi">Item stand: items it takes whatever their type.</param>
    /// <param name="isu">Item stand: items it refuses.</param>
    /// <param name="as">Armour stand: item types per slot.</param>
    /// <param name="cs">Spawn point: the creatures it spawns.</param>
    /// <param name="sa">Nest: the creatures it spawns.</param>
    /// <param name="trig">Spawn point an event wakes.</param>
    /// <param name="rt">Spawn point: respawn time, minutes.</param>
    /// <param name="lv">Spawn point: lowest and highest level.</param>
    /// <param name="day">Spawn point: spawns by day.</param>
    /// <param name="night">Spawn point: spawns by night.</param>
    /// <param name="sr">Nest: radius it spawns in.</param>
    /// <param name="max">Nest: how many it keeps around.</param>
    private sealed record Entry(int p, int d, int t, int c = 0, int i = 0, double gr = 0, int cult = 0, int cw = 0, int ch = 0, double ward = 0, double build = 0, string[]? grows = null,
        int it = -1, int ao = 0, int var = 0, int at = 0, int ats = 0, double dur = 0, double durl = 0, int ms = 1, int[]? @is = null, string[]? isi = null, string[]? isu = null, int[][]? @as = null,
        string[]? cs = null, string[]? sa = null, int trig = 0, double rt = 0, int[]? lv = null, int day = 1, int night = 1, double sr = 0, int max = 0);
}
