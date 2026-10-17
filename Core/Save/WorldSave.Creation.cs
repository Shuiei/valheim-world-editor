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
    /// <see cref="NewObjectBytes(Editing.NewObject, Func{ObjectRef, byte[]})"/> with the builder given (one taken
    /// earlier) instead of the one chosen now.
    /// </summary>
    /// <param name="n">The new object.</param>
    /// <param name="readSource">Gives the chunk file bytes the model lives in.</param>
    /// <param name="builder">The builder a new piece of a kind players build is given (0: none).</param>
    /// <returns>The bytes, or null when the prefab is unknown to both the world and the game.</returns>
    public byte[]? NewObjectBytes(Editing.NewObject n, Func<ObjectRef, byte[]> readSource, long builder)
    {
        byte[]? bytes = BuildBytes(n, readSource);
        if (bytes == null)
        {
            return null;
        }
        // Made here rather than moved or copied with its own fields: placed, pasted or built blank.
        bool made = n.Fresh || n.Raw == null && ModelFor(n.Prefab, n.SourceId) == null;
        string? tool = made ? Terrain.PieceCatalog.Get(n.Prefab)?.Tool : null;
        // A new piece of a kind players build (placed, pasted or built blank) is the chosen builder's,
        // like one built in the game. Without a builder the game takes it for part of a ruin: a third of
        // the materials back, no base for fires, ignored by raids. Moved and edited objects keep theirs.
        bool built = builder != 0 && tool != null;
        // Food and drink set out with the serving tray (Feaster) are items made pieces: the game marks
        // them with "piece" (ItemDrop.MakePiece). Without it they would be loose items, falling and
        // despawning away from a base.
        bool served = tool == "feaster" && Terrain.PrefabCatalog.ItemKindOf(n.Prefab) != null;
        if (!built && !served && n.Data is not { Count: > 0 })
        {
            return bytes;
        }
        // Parsed and written once for all the fields set.
        var z = ZdoData.Parse(bytes);
        if (built)
        {
            z.Set("longs", Editing.ObjectField.CreatorKey, builder.ToString(CultureInfo.InvariantCulture));
        }
        if (served)
        {
            z.Set("ints", PieceKey, "1");
        }
        foreach (var f in n.Data ?? [])
        {
            z.Set(f.Section, f.Key, f.Value);
        }
        return z.Serialize();
    }

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
