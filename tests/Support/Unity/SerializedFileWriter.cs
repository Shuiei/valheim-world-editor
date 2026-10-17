using System.Buffers.Binary;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Writes a Unity serialized file (an assets file, as a bundle holds them), format version 22 as Unity 2022 writes
/// it, little-endian: a header, the metadata (the Unity version, the types with their type trees, the objects' table,
/// the scripts and the other files referenced), then each object's bytes as its type tree lays them out. Only what
/// the editor's readers need, written by the test side so the readers are checked against a writer of their own, with
/// made-up objects (nothing of the game's).
/// </summary>
public sealed partial class SerializedFileWriter
{
    /// <summary>
    /// The file format version written (Unity 2022).
    /// </summary>
    private const uint Version = 22;

    /// <summary>
    /// A type of the file: its Unity class id, its script (for a MonoBehaviour, else -1) and its type tree.
    /// </summary>
    private sealed record TypeEntry(int ClassId, short ScriptIndex, UnityField Tree);

    /// <summary>
    /// An object: its path id, its type's index and its bytes.
    /// </summary>
    private sealed record ObjectEntry(long PathId, int TypeIndex, byte[] Data);

    /// <summary>
    /// The types, in the order they were first used.
    /// </summary>
    private readonly List<TypeEntry> _types = [];
    /// <summary>
    /// The objects, in the order they were added (the order of their bytes in the file).
    /// </summary>
    private readonly List<ObjectEntry> _objects = [];
    /// <summary>
    /// The scripts (MonoScripts) the MonoBehaviours use: each one's file id and path id.
    /// </summary>
    private readonly List<(int FileId, long PathId)> _scripts = [];
    /// <summary>
    /// The other files referenced (a reference's file id is its place here, from 1).
    /// </summary>
    private readonly List<string> _externals = [];
    /// <summary>
    /// The next path id given when none is asked for.
    /// </summary>
    private long _nextId = 1;

    /// <summary>
    /// The Unity version written in the metadata.
    /// </summary>
    public string UnityVersion { get; init; } = "2022.3.62f1";

    /// <summary>
    /// Whether the types' trees are written (Unity leaves them out of a player's own files, such as resources.assets).
    /// </summary>
    public bool TypeTrees { get; init; } = true;

    /// <summary>
    /// Another file references point into (by name, "archive:/CAB-.../CAB-..." for a bundle's file); returns its file
    /// id. Asked twice, the same id.
    /// </summary>
    public int External(string path)
    {
        int i = _externals.IndexOf(path);
        if (i < 0)
        {
            _externals.Add(path);
            i = _externals.Count - 1;
        }
        return i + 1;
    }

    /// <summary>
    /// A script the file's MonoBehaviours can use (a MonoScript, in this file with file id 0, else in an external);
    /// returns its script index.
    /// </summary>
    public short Script(int fileId, long pathId)
    {
        int i = _scripts.IndexOf((fileId, pathId));
        if (i < 0)
        {
            _scripts.Add((fileId, pathId));
            i = _scripts.Count - 1;
        }
        return (short)i;
    }

    /// <summary>
    /// A path id kept for an object added later with it (for references written before the object).
    /// </summary>
    public long Reserve() => _nextId++;

    /// <summary>
    /// Adds an object of this class with these values laid out as <paramref name="tree"/> says. Returns its path id
    /// (the next free one unless <paramref name="pathId"/> is given).
    /// </summary>
    /// <param name="classId">Its Unity class id (UnityClass).</param>
    /// <param name="tree">Its type tree.</param>
    /// <param name="values">Its fields by name; those left out are written as zeros or empty.</param>
    /// <param name="script">A MonoBehaviour's script index (<c>Script</c>); -1 for other classes.</param>
    /// <param name="pathId">Its path id; null for the next free one.</param>
    public long Add(int classId, UnityField tree, Fields values, short script = -1, long? pathId = null)
    {
        int type = _types.FindIndex(t => t.ClassId == classId && t.ScriptIndex == script);
        if (type < 0)
        {
            _types.Add(new TypeEntry(classId, script, tree));
            type = _types.Count - 1;
        }
        long pid = pathId ?? _nextId;
        _nextId = Math.Max(_nextId, pid + 1);
        _objects.Add(new ObjectEntry(pid, type, Serialize(tree, values)));
        return pid;
    }

    /// <summary>
    /// Adds an object whose bytes are given as they are (for objects laid out wrong on purpose), typed as
    /// <paramref name="tree"/>.
    /// </summary>
    public long AddRaw(int classId, UnityField tree, byte[] data, short script = -1, long? pathId = null)
    {
        long pid = Add(classId, tree, [], script, pathId);
        _objects[^1] = _objects[^1] with { Data = data };
        return pid;
    }

    /// <summary>
    /// An object's bytes, laid out as its type tree says.
    /// </summary>
    public static byte[] Serialize(UnityField tree, Fields values)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        SerializedFileValues.Value(w, tree, values);
        return ms.ToArray();
    }

    /// <summary>
    /// The whole file.
    /// </summary>
    public byte[] ToBytes()
    {
        const int HeaderSize = 48;
        // Each object's data starts aligned to 8 bytes.
        long[] offsets = new long[_objects.Count];
        long at = 0;
        for (int i = 0; i < _objects.Count; i++)
        {
            at = Align(at, 8);
            offsets[i] = at;
            at += _objects[i].Data.Length;
        }
        // The objects' table, in the metadata, has their offsets.
        byte[] metadata = Metadata(offsets);
        // The objects' data starts aligned to 16 bytes after the metadata.
        long dataOffset = Align(HeaderSize + metadata.Length, 16);
        long fileSize = dataOffset + at;
        byte[] file = new byte[fileSize];
        var span = file.AsSpan();
        // The header: its first fields big-endian whatever the file's byte order; version 22 has 64-bit sizes after.
        BinaryPrimitives.WriteUInt32BigEndian(span[8..], Version);
        span[16] = 0; // little-endian
        BinaryPrimitives.WriteUInt32BigEndian(span[20..], (uint)metadata.Length);
        BinaryPrimitives.WriteInt64BigEndian(span[24..], fileSize);
        BinaryPrimitives.WriteInt64BigEndian(span[32..], dataOffset);
        metadata.CopyTo(span[HeaderSize..]);
        for (int i = 0; i < _objects.Count; i++)
        {
            _objects[i].Data.CopyTo(span[(int)(dataOffset + offsets[i])..]);
        }
        return file;
    }

    /// <summary>
    /// <paramref name="n"/> rounded up to a multiple of <paramref name="to"/>.
    /// </summary>
    private static long Align(long n, int to) => (n + to - 1) / to * to;
}
