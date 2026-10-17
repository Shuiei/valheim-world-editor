using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>Finding a mesh or material by name (BundleModels.Find.cs).</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// A mesh or material by name. Of several so named, a mesh with that vertex count comes first, then those in
    /// <paramref name="near"/>'s bundle, then the mods' bundles' before the game's. Its id (with <see cref="Mod"/> in
    /// the mods' bundles); null when none has the name.
    /// </summary>
    /// <param name="mods">The mods' bundles, if any.</param>
    /// <param name="kind">Mesh or Material.</param>
    /// <param name="name">The name.</param>
    /// <param name="vertices">A mesh's vertex count (-1: any).</param>
    /// <param name="near">An id's bundle part ("+bundle" or "bundle") whose objects are preferred.</param>
    private (string Id, int Rank)? Find(GameBundles? mods, AssetsTools.NET.Extra.AssetClassID kind, string name, int vertices, string? near = null)
    {
        var all = (mods?.Named(kind, name) ?? []).Select(w => (Id: Mod + w.Key, Game: mods!, At: w))
            .Concat(_game.Named(kind, name).Select(w => (Id: w.Key, Game: _game, At: w))).ToList();
        if (all.Count <= 1)
        {
            return all.Count == 0 ? null : (all[0].Id, 0);
        }
        // Several so named: vertex counts read (the mesh's fields) only then.
        return all.Select((c, i) => (c.Id, Rank: i
                - (near != null && c.Id.StartsWith(near + ":", StringComparison.Ordinal) ? 10_000 : 0)
                - (vertices >= 0 && c.Game.VertexCountOf(c.At) == vertices ? 100_000 : 0)))
            .MinBy(c => c.Rank);
    }
}
