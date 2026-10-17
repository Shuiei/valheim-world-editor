using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>The models of the prefabs (BundleModels.Models.cs): the game's and the mods' own.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// A prefab's model, its parts placed in the view's space; dungeon rooms and locations are read as rooms (without
    /// their saved objects, drawn as objects of their own). Null: none.
    /// </summary>
    public ModelStore.Model? Model(string name)
    {
        bool room = Editing.Dungeons.RoomOf(name) != null || _game.KindOf(name) is { Location: true };
        if (_game.LoadModel(name, room) is not { } m)
        {
            return ModCatalogue.ModelOf(name) is { } reported ? ModModel(name, reported) : null;
        }
        var parts = m.Parts.Select(p => new ModelStore.Part(p.Mesh.Key, p.Sub, p.Material?.Key ?? "", ViewMatrix(p.Matrix))).ToList();
        return new ModelStore.Model(name, parts, new Vector3(m.RootScale[0], m.RootScale[1], m.RootScale[2]));
    }

    /// <summary>
    /// A mod prefab's model as the game reported it: each part's mesh found by name (of those so named, the one with
    /// the vertex count the game had, else the first), in the mods' bundles first, and its material by name, in the
    /// mesh's bundle first; parts whose mesh is nowhere (made while the game runs) are left out.
    /// </summary>
    private ModelStore.Model ModModel(string name, ModCatalogue.Model reported)
    {
        var mods = _mods.Value;
        var parts = new List<ModelStore.Part>();
        foreach (var p in reported.Parts)
        {
            if (FindMesh(mods, p) is not { } mesh)
            {
                continue;
            }
            string near = mesh.Id.Contains(':', StringComparison.Ordinal) ? mesh.Id[..mesh.Id.LastIndexOf(':')] : "";
            string material = p.Material is { } mat && FindMaterial(mods, mat, near) is { } found ? found : "";
            parts.Add(new ModelStore.Part(mesh.Id, p.Sub, material, ViewMatrix(p.Matrix)));
        }
        return new ModelStore.Model(name, parts, new Vector3(reported.RootScale[0], reported.RootScale[1], reported.RootScale[2]));
    }
}
