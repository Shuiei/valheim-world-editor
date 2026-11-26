using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Mods' creation rules (ModExtensions.Create): what a mod does to objects as the game creates them, done to the
/// objects a step makes (placed, pasted, regrown, by any tool, a script or Claude) before the step is made, so it is
/// saved and applied live with them.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// A step's new objects with what the running creation rules do to those it makes: some left out, some with values
    /// set or turned, some added. An object moved (made from a thing the step takes away) or changed where it stands
    /// (standing exactly where a thing the step takes away of its kind stood, turned the same) is not made: it is
    /// passed as it is.
    /// </summary>
    /// <param name="remove">The things the step takes away, indices into the scene's things.</param>
    /// <param name="add">The step's new objects.</param>
    private IReadOnlyList<(NewObject Object, bool Piece)> Created(IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add)
    {
        if (add.Count == 0 || !ModExtensions.HasCreationRules)
        {
            return add;
        }
        var world = Scene.World;
        var removed = remove.Where(i => i >= 0 && i < Scene.Things.Count).Select(i => Scene.Things[i]).ToList();
        var inPlace = removed.Select(t => (t.Prefab, t.Position, t.Rotation)).ToHashSet();
        // A thing moved comes back as a copy of what it was made from (MovedCopy): a saved one's id, or an added one's
        // own making.
        bool Moved(NewObject n) => removed.Any(t => t.Prefab == n.Prefab && (t.Id >= 0
            ? !n.Fresh && n.SourceId == t.Id
            : Edits.FindAdded(t.Id) is { } a && n.SourceId == a.SourceId && ReferenceEquals(n.Raw, a.Raw) && ReferenceEquals(n.Data, a.Data)));
        List<int> made = [.. Enumerable.Range(0, add.Count).Where(i =>
        {
            var n = add[i].Object;
            return !inPlace.Contains((n.Prefab, n.Position, n.Rotation)) && !Moved(n);
        })];
        if (made.Count == 0)
        {
            return add;
        }
        var reader = world.SourceReader();
        var batch = made.Select(i => add[i].Object).Select(n =>
            new ModExtensions.Making(Terrain.PrefabCatalog.NameOf(n.Prefab) ?? "", n.Position, n.Rotation, n.Scale > 0 ? n.Scale : 1)).ToList();
        var area = new SceneLayers.AreaView(Scene, removed.Select(t => t.Id));
        var done = ModExtensions.Create(batch, j => world.NewObjectBytes(add[made[j]].Object, reader) is { } bytes ? ObjectData.Fields(bytes, builder: false) : [],
            world.Seed, area.Biome, area.GroundHeight);
        if (done is not { None: false })
        {
            return add;
        }
        var result = add.ToList();
        foreach (var (j, change) in done.Changed)
        {
            int i = made[j];
            var n = result[i].Object;
            result[i] = (n with { Rotation = change.Rotation, Data = change.Values.Count > 0 ? [.. n.Data ?? [], .. change.Values] : n.Data }, result[i].Piece);
        }
        foreach (int j in done.Removed.OrderDescending())
        {
            result.RemoveAt(made[j]);
        }
        foreach (var obj in done.Added)
        {
            int hash = StableHash.Of(obj.Prefab);
            if (Terrain.PrefabCatalog.Get(hash) == null)
            {
                Console.WriteLine($"a mod's creation rules added {obj.Prefab}, which is no object of the game (left out)");
                continue;
            }
            result.Add((new NewObject(0, hash, obj.Position, obj.Rotation, 0, Data: obj.Data.Count > 0 ? [.. obj.Data] : null), false));
        }
        return result;
    }
}
