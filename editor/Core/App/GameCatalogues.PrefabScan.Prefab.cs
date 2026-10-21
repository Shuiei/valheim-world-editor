using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class PrefabScan
    {
        /// <summary>
        /// Adds the entry of a networked root (its name and components), when its ZNetView can be read; the first
        /// root of a name is kept. A spawn point (CreatureSpawner) also gets its respawn time in minutes ("rt", 0
        /// when it spawns once), its level range ("lv": min, max) and when it spawns ("day", "night": 0 when it
        /// does not); a spawn point an event wakes (TriggerSpawner) is marked ("trig"); a nest (SpawnArea) gets
        /// the radius it spawns in ("sr") and how many it keeps around ("max"). The creatures they name are filled
        /// in by <c>Json</c>.
        /// </summary>
        private void ReadPrefab(Bundle b, Bundle.Inner f, Ref at, (string Name, (int File, long Pid)[] Components) go)
        {
            var parts = ReadComponents(b, f, go.Components);
            if (parts.Net is not { } znv)
            {
                return;
            }
            string name = go.Name;
            _names[at] = name;
            if (_prefabs.ContainsKey(name))
            {
                return;
            }
            var entry = PrefabEntry(znv, parts);
            if (parts.Spawns is { } spawns)
            {
                _spawns[name] = spawns;
            }
            _prefabs[name] = entry;
        }

        /// <summary>
        /// A networked root's entry: its ZNetView's flags and type, then what its components add (see <c>ReadPrefab</c>).
        /// </summary>
        private static Dict PrefabEntry(AssetTypeValueField znv, RootComponents parts)
        {
            var entry = new Dict { ["p"] = Int(Py(znv["m_persistent"])), ["d"] = Int(Py(znv["m_distant"])), ["t"] = Int(Py(znv["m_type"])) };
            if (Truthy(Get(znv, "m_syncInitialScale", 0L)))
            {
                entry["s"] = 1L;
            }
            if (parts.Creature)
            {
                entry["c"] = 1L;
            }
            if (parts.Item)
            {
                entry["i"] = 1L;
            }
            if (parts.Plant is { } plant)
            {
                entry["gr"] = R(Num(Py(plant["m_growRadius"])), 3);
                entry["cult"] = Int(Get(plant, "m_needCultivatedGround", 0L));
            }
            foreach (var (k, v) in parts.Extra)
            {
                entry[k] = v;
            }
            return entry;
        }
    }
}
