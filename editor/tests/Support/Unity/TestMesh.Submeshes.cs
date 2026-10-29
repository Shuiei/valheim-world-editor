namespace WorldEditor.Tests.Unity;

/// <summary>
/// The submeshes of a made-up mesh: their index buffer and their Unity records.
/// </summary>
public sealed partial class TestMesh
{
    /// <summary>
    /// The submeshes' index buffer and their Unity records (the first byte and count of each one's indices).
    /// </summary>
    private (List<byte> Indices, List<Fields> Subs) SubmeshData()
    {
        var indices = new List<byte>();
        var subs = new List<Fields>();
        for (int i = 0; i < Submeshes.Length; i++)
        {
            subs.Add(new Fields
            {
                ["firstByte"] = (uint)indices.Count,
                ["indexCount"] = (uint)Submeshes[i].Length,
                ["topology"] = Topology?[i] ?? 0,
                ["baseVertex"] = (uint)(BaseVertex?[i] ?? 0),
            });
            foreach (int k in Submeshes[i])
            {
                indices.AddRange(Wide ? BitConverter.GetBytes((uint)k) : BitConverter.GetBytes((ushort)k));
            }
        }
        return (indices, subs);
    }
}
