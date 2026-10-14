using System.Buffers.Binary;
using System.Collections;
using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Writes a Unity serialized file (an assets file, as a bundle holds them), format version 22 as Unity 2022 writes
/// it, little-endian: a header, the metadata (the Unity version, the types with their type trees, the objects' table,
/// the scripts and the other files referenced), then each object's bytes as its type tree lays them out. Only what
/// the editor's readers need, written by the test side so the readers are checked against a writer of their own, with
/// made-up objects (nothing of the game's).
/// </summary>
public sealed class SerializedFileWriter
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
        Value(w, tree, values);
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
    /// The metadata: Unity version, platform, the types, the objects' table (with these offsets from the data's start),
    /// the scripts, the externals, no reference types, no user information.
    /// </summary>
    private byte[] Metadata(long[] offsets)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        CString(w, UnityVersion);
        w.Write(19); // StandaloneWindows64
        w.Write(TypeTrees);
        w.Write(_types.Count);
        foreach (var t in _types)
        {
            w.Write(t.ClassId);
            w.Write((byte)0); // not stripped
            w.Write(t.ScriptIndex);
            if (t.ClassId == UnityClass.MonoBehaviour)
            {
                w.Write(new byte[16]); // the script's hash
            }
            w.Write(new byte[16]); // the type's hash
            if (TypeTrees)
            {
                TypeTree(w, t.Tree);
                w.Write(0); // no type dependencies
            }
        }
        w.Write(_objects.Count);
        Pad(w);
        for (int i = 0; i < _objects.Count; i++)
        {
            var o = _objects[i];
            w.Write(o.PathId);
            w.Write(offsets[i]);
            w.Write((uint)o.Data.Length);
            w.Write(o.TypeIndex);
        }
        w.Write(_scripts.Count);
        foreach (var (fileId, pathId) in _scripts)
        {
            w.Write(fileId);
            Pad(w);
            w.Write(pathId);
        }
        w.Write(_externals.Count);
        foreach (string e in _externals)
        {
            CString(w, "");
            w.Write(new byte[16]); // GUID
            w.Write(0); // a serialized file
            CString(w, e);
        }
        w.Write(0); // no reference types
        CString(w, "");
        return ms.ToArray();
    }

    /// <summary>
    /// A type tree blob: its nodes depth first (32 bytes each), then their names in a local string buffer.
    /// </summary>
    private static void TypeTree(BinaryWriter w, UnityField root)
    {
        var strings = new MemoryStream();
        var offsets = new Dictionary<string, uint>(StringComparer.Ordinal);
        uint Offset(string s)
        {
            if (!offsets.TryGetValue(s, out uint o))
            {
                offsets[s] = o = (uint)strings.Length;
                strings.Write(Encoding.UTF8.GetBytes(s + "\0"));
            }
            return o;
        }
        var nodes = new List<(UnityField Field, int Level)>();
        void Walk(UnityField f, int level)
        {
            nodes.Add((f, level));
            foreach (var c in f.Children)
            {
                Walk(c, level + 1);
            }
        }
        Walk(root, 0);
        using var nodeBytes = new MemoryStream();
        using (var nw = new BinaryWriter(nodeBytes, Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var (f, level) = nodes[i];
                nw.Write((ushort)1);
                nw.Write((byte)level);
                nw.Write((byte)(f.IsArray ? 1 : 0));
                nw.Write(Offset(f.Type));
                nw.Write(Offset(f.Name));
                nw.Write(f.Size > 0 ? f.Size : -1);
                nw.Write(i);
                nw.Write(f.Align ? 0x4000u : 0u);
                nw.Write(0UL); // no reference type hash
            }
        }
        w.Write(nodes.Count);
        w.Write((int)strings.Length);
        w.Write(nodeBytes.ToArray());
        w.Write(strings.ToArray());
    }

    /// <summary>
    /// One value laid out as its field says: a structure field after field (missing ones as zeros), a list or text as
    /// its count then its items, a number in its size; padded after a field marked so.
    /// </summary>
    private static void Value(BinaryWriter w, UnityField f, object? v)
    {
        if (f.Type == "string")
        {
            byte[] b = Encoding.UTF8.GetBytes(v as string ?? "");
            w.Write(b.Length);
            w.Write(b);
            if (f.Children.Count > 0 && f.Children[0].Align)
            {
                Pad(w);
            }
        }
        else if (f.IsArray)
        {
            var item = f.Children[1];
            if (v is byte[] bytes)
            {
                w.Write(bytes.Length);
                w.Write(bytes);
            }
            else
            {
                var items = v as IList ?? (v is IEnumerable e and not string ? e.Cast<object?>().ToList() : []);
                w.Write(items.Count);
                foreach (object? it in items)
                {
                    Value(w, item, it);
                }
            }
        }
        else if (f.Children.Count > 0)
        {
            var d = v as Fields;
            foreach (var c in f.Children)
            {
                // A list's array node takes the list's own value.
                Value(w, c, c.IsArray && f.Children.Count == 1 ? v : d?.GetValueOrDefault(c.Name));
            }
        }
        else
        {
            Number(w, f.Type, v);
        }
        if (f.Align)
        {
            Pad(w);
        }
    }

    /// <summary>
    /// A number of a Unity type (null: 0).
    /// </summary>
    private static void Number(BinaryWriter w, string type, object? v)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        switch (type)
        {
            case "bool":
                w.Write(Convert.ToBoolean(v ?? false, c));
                break;
            case "UInt8" or "char":
                w.Write(Convert.ToByte(v ?? 0, c));
                break;
            case "SInt8":
                w.Write(Convert.ToSByte(v ?? 0, c));
                break;
            case "UInt16" or "unsigned short":
                w.Write(Convert.ToUInt16(v ?? 0, c));
                break;
            case "SInt16" or "short":
                w.Write(Convert.ToInt16(v ?? 0, c));
                break;
            case "int" or "SInt32":
                w.Write(Convert.ToInt32(v ?? 0, c));
                break;
            case "unsigned int" or "UInt32":
                w.Write(Convert.ToUInt32(v ?? 0, c));
                break;
            case "SInt64" or "long long":
                w.Write(Convert.ToInt64(v ?? 0, c));
                break;
            case "UInt64" or "FileSize":
                w.Write(Convert.ToUInt64(v ?? 0, c));
                break;
            case "float":
                w.Write(Convert.ToSingle(v ?? 0f, c));
                break;
            case "double":
                w.Write(Convert.ToDouble(v ?? 0.0, c));
                break;
            default:
                throw new ArgumentException($"no number type {type}");
        }
    }

    /// <summary>
    /// A nul-terminated UTF-8 text.
    /// </summary>
    private static void CString(BinaryWriter w, string s)
    {
        w.Write(Encoding.UTF8.GetBytes(s));
        w.Write((byte)0);
    }

    /// <summary>
    /// Zeros up to the next multiple of 4 bytes.
    /// </summary>
    private static void Pad(BinaryWriter w)
    {
        while (w.BaseStream.Position % 4 != 0)
        {
            w.Write((byte)0);
        }
    }

    /// <summary>
    /// <paramref name="n"/> rounded up to a multiple of <paramref name="to"/>.
    /// </summary>
    private static long Align(long n, int to) => (n + to - 1) / to * to;
}
