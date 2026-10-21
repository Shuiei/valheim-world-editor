using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The settings of the game's terrain operations (the TerrainOp of each hoe and cultivator piece), by the name of
    /// the object they are on, as Tunnelheim.TerrainOps takes them: Tunnelheim's records name a stroke on rock by that
    /// name's hash and replay it with the game's settings. Read at the editor's start (GameData).
    /// </summary>
    private sealed class TerrainOpScan : IStartScan
    {
        /// <summary>The settings' fields kept, as the game names them.</summary>
        private static readonly string[] Fields =
        [
            "m_levelOffset", "m_level", "m_levelRadius", "m_square", "m_raise", "m_raiseRadius", "m_raisePower", "m_raiseDelta",
            "m_smooth", "m_smoothRadius", "m_smoothPower", "m_paintCleared", "m_paintType", "m_paintRadius",
        ];

        /// <summary>The entries by object name: each setting by its field's name.</summary>
        public Dict Entries { get; } = new();

        /// <summary>The "terrain-ops" JSON.</summary>
        public string Json() => CatalogueJson.Write(Entries, new CatalogueJson.Style(",", Indent: 0));

        /// <summary>Reads every TerrainOp of a bundle; the first of a name is kept, as every scan takes it.</summary>
        public void Read(Bundle b, Game game)
        {
            foreach (var (f, info) in b.Objects)
            {
                if (info.TypeId != MonoBehaviourType || game.ScriptClass(b, f, info) != "TerrainOp" || b.Field(f, info.PathId) is not { } m
                    || b.Field(m["m_GameObject.m_PathID"].AsLong) is not { } go || !Has(m, "m_settings"))
                {
                    continue;
                }
                var s = m["m_settings"];
                var entry = new Dict();
                foreach (string name in Fields)
                {
                    entry[name] = Py(s[name]);
                }
                Entries.TryAdd(go["m_Name"].AsString, entry);
            }
        }
    }
}
