using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>The mesh a mod's part is drawn with (BundleModels.Mods.cs), found by name and bounds.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// A part's mesh: of the meshes so named (the mods' bundles first, then the game's), one with the bounds the game
    /// told (when it told them) and its vertex count; else Unity's own primitive of that name and bounds; else, the game
    /// not having told the bounds, the one with the vertex count or the first. Its id; null when there is none.
    /// </summary>
    private (string Id, int Rank)? FindMesh(GameBundles? mods, ModCatalogue.ModelPart p)
    {
        if (p.Bounds is not { } bounds)
        {
            return Find(mods, AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh, p.Vertices);
        }
        var fits = (mods?.Named(AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh) ?? []).Select(w => (Id: Mod + w.Key, Game: mods!, At: w))
            .Concat(_game.Named(AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh).Select(w => (Id: w.Key, Game: _game, At: w)))
            .Where(c => c.Game.BoundsOf(c.At) is { } b && SameBounds(b, bounds))
            .Select((c, i) => (c.Id, Rank: i - (c.Game.VertexCountOf(c.At) == p.Vertices ? 100_000 : 0)))
            .ToList();
        if (fits.Count > 0)
        {
            return fits.MinBy(c => c.Rank);
        }
        return Primitives.TryGetValue(p.Mesh, out float[]? own) && SameBounds(own, bounds) ? (Primitive + p.Mesh, 0) : null;
    }

    /// <summary>Whether two bounds (centre and extent) are the same, to a centimetre or a hundredth of their size.</summary>
    private static bool SameBounds(float[] a, float[] b)
    {
        float size = Math.Max(Math.Max(Math.Abs(b[3]), Math.Abs(b[4])), Math.Abs(b[5]));
        float tolerance = Math.Max(0.01f, size * 0.01f);
        for (int i = 0; i < 6; i++)
        {
            if (Math.Abs(a[i] - b[i]) > tolerance)
            {
                return false;
            }
        }
        return true;
    }
}
