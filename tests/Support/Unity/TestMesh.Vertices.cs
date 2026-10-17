namespace WorldEditor.Tests.Unity;

/// <summary>
/// The vertex formats and the vertex bytes of a made-up mesh, for <see cref="TestMesh"/>.
/// </summary>
public sealed partial class TestMesh
{
    /// <summary>
    /// The bytes per component of a format.
    /// </summary>
    private static int SizeOf(VertexFormat format) => format switch
    {
        VertexFormat.Ieee32 => 4,
        VertexFormat.Ieee16 or VertexFormat.UNorm16 or VertexFormat.SNorm16 => 2,
        _ => 1,
    };

    /// <summary>
    /// A value in a format, as bytes.
    /// </summary>
    private static byte[] Encode(float v, VertexFormat format) => format switch
    {
        VertexFormat.Ieee32 => BitConverter.GetBytes(v),
        VertexFormat.Ieee16 => BitConverter.GetBytes((Half)v),
        VertexFormat.UNorm8 => [(byte)Math.Round(v * 255)],
        VertexFormat.SNorm8 => [(byte)(sbyte)Math.Round(v * 127)],
        VertexFormat.UNorm16 => BitConverter.GetBytes((ushort)Math.Round(v * 65535)),
        VertexFormat.SNorm16 => BitConverter.GetBytes((short)Math.Round(v * 32767)),
        _ => throw new ArgumentException($"no vertex format {format}"),
    };

    /// <summary>
    /// The vertex bytes of each stream: its channels' values for each vertex, the streams each 16-byte aligned.
    /// </summary>
    private static List<byte> VertexBytes(List<(int Channel, int Stream, VertexFormat Format, int Dim, float[] Values)> used, int streams, int n)
    {
        var data = new List<byte>();
        for (int s = 0; s < streams; s++)
        {
            while (data.Count % 16 != 0)
            {
                data.Add(0);
            }
            for (int v = 0; v < n; v++)
            {
                foreach (var c in used.Where(c => c.Stream == s))
                {
                    for (int d = 0; d < c.Dim; d++)
                    {
                        data.AddRange(Encode(c.Values[v * c.Dim + d], c.Format));
                    }
                }
            }
        }
        return data;
    }
}
