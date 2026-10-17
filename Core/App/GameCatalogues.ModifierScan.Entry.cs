using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;
using V3 = (double X, double Y, double Z);

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class ModifierScan
    {
        /// <summary>
        /// One modifier as the catalogue has it (pos: where it is in the prefab's frame).
        /// </summary>
        private static Dict Entry(AssetTypeValueField m, bool atRoot, V3 p) => new()
        {
            ["pos"] = atRoot ? Origin() : [R(p.X, 4), R(p.Y, 4), R(p.Z, 4)],
            ["level"] = Py(m["m_level"]),
            ["levelOffset"] = Py(m["m_levelOffset"]),
            ["levelRadius"] = Py(m["m_levelRadius"]),
            ["square"] = Py(m["m_square"]),
            ["smooth"] = Py(m["m_smooth"]),
            ["smoothRadius"] = Py(m["m_smoothRadius"]),
            ["smoothPower"] = Py(m["m_smoothPower"]),
            ["paintCleared"] = Py(m["m_paintCleared"]),
            ["paintType"] = Py(m["m_paintType"]),
            ["paintRadius"] = Py(m["m_paintRadius"]),
            ["paintStrength"] = Get(m, "m_paintStrength", 1.0),
            ["paintHeightCheck"] = Py(m["m_paintHeightCheck"]),
            ["sortOrder"] = Py(m["m_sortOrder"]),
            ["player"] = Py(m["m_playerModifiction"]),
            ["useTerrainCompiler"] = Get(m, "m_useTerrainCompiler", 0L),
        };
    }
}
