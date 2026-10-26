using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The cave roof rocks' shapes, each prefab's mesh in its own frame (the model's scale applied: its
    /// rock is a child scaled 4 times), as vertices and triangles, for the Path tool's Cave (CaveRoof),
    /// read at the editor's start (GameData): only the bundle that has them is opened (the index tells it).
    /// </summary>
    public static string CaveRocksJson(GameBundles bundles) => CatalogueJson.Write(CaveRocks(bundles), new CatalogueJson.Style(",", ":"));

    /// <summary>
    /// The two cave roof rocks' shapes, by prefab name.
    /// </summary>
    private static Dict CaveRocks(GameBundles bundles)
    {
        var res = new Dict();
        using var game = new Game(bundles.Folder);
        // Each bundle opened once (both rocks are in the same one).
        var open = new Dictionary<string, Bundle>(StringComparer.Ordinal);
        try
        {
            foreach (var (prefab, mesh, scale) in new[] { ("rock4_forest", "Rock4", 4.0), ("rock3_mountain", "Rock3", 4.0) })
            {
                if (!bundles.Index.TryGetValue(prefab, out var at))
                {
                    throw new InvalidDataException($"{prefab} is not in the game's files");
                }
                if (!open.TryGetValue(at.Bundle, out var b))
                {
                    open[at.Bundle] = b = game.Open(at.Bundle) ?? throw new InvalidDataException($"{at.Bundle} cannot be read");
                }
                res[prefab] = RockShape(b, prefab, mesh, scale);
            }
        }
        finally
        {
            foreach (var b in open.Values)
            {
                b.Dispose();
            }
        }
        return res;
    }

    /// <summary>
    /// One rock's mesh (named mesh in its prefab's bundle), scaled, as cave-rocks has it.
    /// </summary>
    private static Dict RockShape(Bundle b, string prefab, string mesh, double scale)
    {
        // Told by its name, read alone first (the bundle has many meshes).
        var m = b.Objects.Where(o => o.Info.TypeId == MeshType && NameOf(o.File, o.Info) == mesh).Select(o => b.Field(o.File, o.Info.PathId)).FirstOrDefault(m => m != null);
        var (vertices, triangles) = (m == null ? null : MeshData(b, m)) ?? throw new InvalidDataException($"{prefab}'s mesh {mesh} cannot be read");
        // As written to an OBJ file and read back (UnityPy's export: 9 significant digits, x mirrored and
        // back, triangles wound the other way).
        var vs = new List<object?>();
        for (int i = 0; i + 2 < vertices.Length; i += 3)
        {
            vs.Add(new List<object?> { R(-CatalogueJson.Significant9(-(double)vertices[i]) * scale, 3), R(CatalogueJson.Significant9(vertices[i + 1]) * scale, 3), R(CatalogueJson.Significant9(vertices[i + 2]) * scale, 3) });
        }
        var tris = new List<object?>();
        foreach (int[] sub in triangles)
        {
            for (int k = 0; k + 2 < sub.Length; k += 3)
            {
                tris.Add((long)sub[k + 2]);
                tris.Add((long)sub[k + 1]);
                tris.Add((long)sub[k]);
            }
        }
        return new Dict { ["v"] = vs, ["t"] = tris };
    }

    /// <summary>
    /// A named object's name (m_Name, its first field), nothing else read.
    /// </summary>
    private static string? NameOf(Bundle.Inner f, AssetFileInfo info)
    {
        var file = f.Assets.file;
        file.Reader.Position = info.GetAbsoluteByteOffset(file);
        int n = file.Reader.ReadInt32();
        return n is >= 0 and <= 256 ? System.Text.Encoding.UTF8.GetString(file.Reader.ReadBytes(n)) : null;
    }
}
