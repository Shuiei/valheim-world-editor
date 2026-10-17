using System.Globalization;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>Makes an object the template of its prefab when it has none yet.</summary>
    private void RememberTemplate(int prefab, int id)
    {
        if (Templates.TryAdd(prefab, id))
        {
            TemplatesGeneration++;
        }
    }

    /// <summary>
    /// The object a new one is built from: its source object when given (and of the same prefab), else the prefab's
    /// template. null when the world has no object of the prefab: it is then built blank (NewObjectBytes).
    /// </summary>
    public ObjectRef? ModelFor(int prefab, int? sourceId)
    {
        if (sourceId is int s && s >= 0 && s < ObjectRefs.Count && ObjectRefs[s].Prefab == prefab)
        {
            return ObjectRefs[s];
        }
        return Templates.TryGetValue(prefab, out int t) ? ObjectRefs[t] : null;
    }

    /// <summary>
    /// Whether the editor can make an object of this prefab: the world has one to copy, or the game's catalogue knows
    /// it (placeable prefab or creature).
    /// </summary>
    public bool CanCreate(int prefab) => Templates.ContainsKey(prefab) || Terrain.PrefabCatalog.Get(prefab) != null || Terrain.PrefabCatalog.IsCreature(prefab);

    /// <summary>Prefabs that can be created: any object in the world, and every placeable game prefab.</summary>
    public IEnumerable<int> Creatable => Templates.Keys.Concat(Terrain.PrefabCatalog.Placeable.Select(p => StableHash.Of(p.Name))).Distinct();

    /// <summary>
    /// The save bytes of a new object: a copy of its model when the world has one, else a blank object with the
    /// prefab's own flags from the game (what the game writes for a freshly placed object).
    /// </summary>
    /// <param name="n">The new object.</param>
    /// <param name="readSource">Gives the chunk file bytes the model lives in.</param>
    /// <returns>The bytes, or null when the prefab is unknown to both the world and the game.</returns>
    public byte[]? NewObjectBytes(Editing.NewObject n, Func<ObjectRef, byte[]> readSource) => NewObjectBytes(n, readSource, Builder);

    /// <summary>
    /// The new object's bytes before the builder and data fields: its raw bytes moved, else a copy of its model, else a
    /// blank object from the catalogue (null when the game does not know the prefab).
    /// </summary>
    private byte[]? BuildBytes(Editing.NewObject n, Func<ObjectRef, byte[]> readSource)
    {
        if (n.Raw != null)
        {
            var z = ZdoData.Parse(n.Raw);
            z.Position = n.Position;
            z.Rotation = n.Rotation;
            if (n.Scale > 0f)
            {
                z.Set("vec3", ZdoData.ScaleKey, $"{n.Scale.ToString(CultureInfo.InvariantCulture)} {n.Scale.ToString(CultureInfo.InvariantCulture)} {n.Scale.ToString(CultureInfo.InvariantCulture)}");
                z.Set("floats", ZdoData.ScaleScalarKey, null);
            }
            return z.Serialize();
        }
        ObjectRef? model = ModelFor(n.Prefab, n.SourceId);
        if (model != null)
        {
            return ZdoBuilder.Build(readSource(model), model, model.File.WorldVersion, n.Position, n.Rotation, n.Scale, n.Fresh);
        }
        // Also what is not offered for placing (creatures: generated dungeons put some down).
        return Terrain.PrefabCatalog.Details(n.Prefab) is { } info ? ZdoBuilder.Blank(n.Prefab, info.Flags, n.Position, n.Rotation, n.Scale) : null;
    }
}
