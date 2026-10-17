using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>Finding a mod's material for a game's one (BundleModels.Materials.cs).</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// What mods name the game's assets they stand in for until the game runs: Jötunn's placeholders ("JVLmock_"), and
    /// the materials a mod swaps for the game's of the same name ("_REPLACE_").
    /// </summary>
    private static readonly string[] Placeholders = ["JVLmock_", "_REPLACE_"];

    /// <summary>
    /// A material by name (see <see cref="Find"/>), but never a mod's placeholder where the game has the real one: a
    /// placeholder's name (a <see cref="Placeholders"/> prefix) is looked up without its prefix, and a mod's material of
    /// the game's name whose shader is a Jötunn placeholder counts only when the game has none so named.
    /// </summary>
    private string? FindMaterial(GameBundles? mods, string name, string near)
    {
        if (Placeholders.FirstOrDefault(p => name.StartsWith(p, StringComparison.Ordinal)) is { } prefix)
        {
            var real = _game.Named(AssetsTools.NET.Extra.AssetClassID.Material, name[prefix.Length..]);
            return real.Count > 0 ? real[0].Key : Find(mods, AssetsTools.NET.Extra.AssetClassID.Material, name, -1, near)?.Id;
        }
        var named = _game.Named(AssetsTools.NET.Extra.AssetClassID.Material, name);
        var game = named.Count > 0 ? named[0] : null;
        bool Real(GameBundles.Where w) => !(mods!.LoadMaterial(w)?.Shader.StartsWith(Placeholders[0], StringComparison.Ordinal) ?? false);
        var own = game == null ? null : (mods?.Named(AssetsTools.NET.Extra.AssetClassID.Material, name) ?? []).Where(Real).ToList();
        return game != null && own!.Count == 0 ? game.Key : Find(mods, AssetsTools.NET.Extra.AssetClassID.Material, name, -1, near)?.Id;
    }
}
