using TerrainEditor.App;
using TerrainEditor.Editing;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// Mods' placers (ModExtensions.Place): what a mod works out from where its objects stand, set on the objects a step
/// places, pastes, moves or turns, before the step is made (so it is saved and applied live with them).
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A step's new objects with what the running placers set on those of their prefabs, as values added to each one's
    /// own (applied last, whatever it is made from). An object standing exactly where a thing the step takes away of
    /// its kind stood, turned the same, is changed in place (the inspector, a tool's edit): no placement.
    /// </summary>
    /// <param name="remove">The things the step takes away, indices into the scene's things.</param>
    /// <param name="add">The step's new objects.</param>
    private IReadOnlyList<(NewObject Object, bool Piece)> Placed(IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add)
    {
        if (add.Count == 0 || Scene.World is not { } world || !add.Any(a => ModExtensions.HasPlacer(a.Object.Prefab)))
        {
            return add;
        }
        var removed = remove.Where(i => i >= 0 && i < Scene.Things.Count).Select(i => Scene.Things[i]).ToList();
        var inPlace = removed.Select(t => (t.Prefab, t.Position, t.Rotation)).ToHashSet();
        var reader = world.SourceReader();
        var views = new List<(int Index, List<ObjectField> Before, CopyView View)>();
        for (int i = 0; i < add.Count; i++)
        {
            var n = add[i].Object;
            if (!ModExtensions.HasPlacer(n.Prefab) || inPlace.Contains((n.Prefab, n.Position, n.Rotation))
                || world.NewObjectBytes(n, reader) is not { } bytes)
            {
                continue;
            }
            var fields = ObjectData.Fields(bytes, builder: true);
            string name = ModExtensions.PlacedName(n.Prefab) ?? Terrain.PrefabCatalog.NameOf(n.Prefab) ?? "";
            views.Add((i, [.. fields], new CopyView(name, n.Position, n.Rotation, fields, n.Scale)));
        }
        if (views.Count == 0)
        {
            return add;
        }
        ModExtensions.Place([.. views.Select(v => (add[v.Index].Object.Prefab, (ICopy)v.View))], new SceneLayers.AreaView(Scene, removed.Select(t => t.Id)));
        var placed = add.ToList();
        foreach (var (index, before, view) in views)
        {
            var changes = view.Data.Where(f => !before.Contains(f)).ToList();
            if (changes.Count > 0)
            {
                var n = placed[index].Object;
                placed[index] = (n with { Data = [.. n.Data ?? [], .. changes] }, placed[index].Piece);
            }
        }
        return placed;
    }
}
