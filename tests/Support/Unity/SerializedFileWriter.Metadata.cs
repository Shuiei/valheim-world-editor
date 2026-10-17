using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// The metadata of a written serialized file: the Unity version, the types, the objects' table, the scripts and the
/// externals, each as the readers expect it.
/// </summary>
public sealed partial class SerializedFileWriter
{
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
                SerializedFileTypeTree.Write(w, t.Tree);
                w.Write(0); // no type dependencies
            }
        }
        w.Write(_objects.Count);
        SerializedFileValues.Pad(w);
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
            SerializedFileValues.Pad(w);
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
    /// A nul-terminated UTF-8 text.
    /// </summary>
    private static void CString(BinaryWriter w, string s)
    {
        w.Write(Encoding.UTF8.GetBytes(s));
        w.Write((byte)0);
    }
}
