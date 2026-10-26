using System.Globalization;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
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
        return WithFields(bytes, n, builder, built, served);
    }

    /// <summary>Sets the builder, the piece mark and the object's data fields on its bytes (parsed and written once).</summary>
    /// <param name="bytes">The object's bytes.</param>
    /// <param name="n">The new object (its data fields).</param>
    /// <param name="builder">The builder to set when <paramref name="built"/>.</param>
    /// <param name="built">Whether the builder is set.</param>
    /// <param name="served">Whether the piece mark is set.</param>
    /// <returns>The bytes with the fields set.</returns>
    private static byte[] WithFields(byte[] bytes, Editing.NewObject n, long builder, bool built, bool served)
    {
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
}
