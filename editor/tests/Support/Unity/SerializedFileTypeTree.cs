using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Writes a serialized file's type tree blob (its nodes depth first, then their names), for
/// <see cref="SerializedFileWriter"/>.
/// </summary>
internal static class SerializedFileTypeTree
{
    /// <summary>
    /// A type tree blob: its nodes depth first (32 bytes each), then their names in a local string buffer.
    /// </summary>
    internal static void Write(BinaryWriter w, UnityField root)
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
}
