
namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// A reader of the bytes an object's offsets point into, for <see cref="NewObjectBytes(Editing.NewObject, Func{ObjectRef, byte[]}, long)"/>: live, its own
    /// (<see cref="LiveSource"/>: the snapshot, or the bytes of a read since); else its whole chunk file, read once per
    /// reader.
    /// </summary>
    public Func<ObjectRef, byte[]> SourceReader()
    {
        if (LiveBytes != null)
        {
            return o => LiveSource(o.File);
        }
        Dictionary<ChunkFile, byte[]> files = [];
        return o => files.TryGetValue(o.File, out byte[]? f) ? f : files[o.File] = File.ReadAllBytes(Path.Combine(Directory, o.File.FileName));
    }

    /// <summary>
    /// The saved bytes of an object of the save (id &gt;= 0): from its chunk file, or from the live snapshot.
    /// </summary>
    public byte[] ObjectBytes(int id)
    {
        ObjectRef o = ObjectRefs[id];
        if (LiveBytes != null)
        {
            return LiveSource(o.File)[(int)o.Start..(int)o.End];
        }
        using FileStream fs = File.OpenRead(Path.Combine(Directory, o.File.FileName));
        byte[] b = new byte[o.End - o.Start];
        fs.Seek(o.Start, SeekOrigin.Begin);
        fs.ReadExactly(b);
        return b;
    }

    /// <summary>
    /// The saved bytes of an object of the save (id &gt;= 0), cut out of what a <see cref="SourceReader"/> gives: for
    /// many objects, each chunk file is read once instead of opened for every object.
    /// </summary>
    public byte[] ObjectBytes(int id, Func<ObjectRef, byte[]> source)
    {
        ObjectRef o = ObjectRefs[id];
        return source(o)[(int)o.Start..(int)o.End];
    }

    /// <summary>
    /// An object's saved bytes as <see cref="ObjectBytes(int)"/> gives them, live without a copy: a view into the bytes it
    /// was read from.
    /// </summary>
    private ReadOnlySpan<byte> SavedSpan(int id)
    {
        if (LiveBytes == null)
        {
            return ObjectBytes(id);
        }
        ObjectRef o = ObjectRefs[id];
        return LiveSource(o.File).AsSpan((int)o.Start, (int)(o.End - o.Start));
    }

    /// <summary>Generated zones and location instances from the .db2 file (null if it could not be read).</summary>
    public ZoneDb? Zones { get; private set; }
}
