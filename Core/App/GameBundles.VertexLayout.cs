using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// A mesh's vertex layout: the channels of its vertices and how they are read.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Bytes per component, by Unity's VertexFormat (Float, Float16, UNorm8, SNorm8, UNorm16, SNorm16, UInt8, SInt8,
    /// UInt16, SInt16, UInt32, SInt32).
    /// </summary>
    private static readonly int[] FormatSize = [4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4];

    /// <summary>
    /// A mesh's vertex layout, for every reader of its vertex data: its channels, and each stream's start in the data
    /// and bytes per vertex. Streams follow one another, each starting 16-byte aligned; in a stream a vertex's channels
    /// are side by side.
    /// </summary>
    /// <param name="Channels">The channels (stream, offset in the stream's vertex, Unity's VertexFormat, dimension; 0
    /// for none), in Unity's order: position, normal, tangent, colour, uv0...</param>
    /// <param name="Count">The number of vertices.</param>
    /// <param name="Start">Each stream's first byte.</param>
    /// <param name="Stride">Each stream's bytes per vertex.</param>
    internal sealed record VertexLayout(List<(int Stream, int Offset, int Format, int Dim)> Channels, int Count, int[] Start, int[] Stride)
    {
        /// <summary>Whether a channel's format is one read here (its size is known).</summary>
        public static bool Known(int format) => format >= 0 && format < FormatSize.Length;

        /// <summary>The layout a mesh's m_VertexData describes (channels of an unknown format take no room).</summary>
        public static VertexLayout Of(AssetTypeValueField vd)
        {
            int n = (int)vd["m_VertexCount"].AsUInt;
            var channels = vd["m_Channels.Array"].Children.Select(c => (Stream: (int)c["stream"].AsByte, Offset: (int)c["offset"].AsByte, Format: (int)c["format"].AsByte, Dim: c["dimension"].AsByte & 0xF)).ToList();
            var used = channels.Where(c => c.Dim > 0 && Known(c.Format)).ToList();
            int streams = used.Select(c => c.Stream).DefaultIfEmpty(0).Max() + 1;
            int[] stride = new int[streams];
            foreach (var c in used)
            {
                stride[c.Stream] = Math.Max(stride[c.Stream], c.Offset + c.Dim * FormatSize[c.Format]);
            }
            int[] start = new int[streams];
            for (int s = 1; s < streams; s++)
            {
                start[s] = (start[s - 1] + stride[s - 1] * n + 15) & ~15;
            }
            return new VertexLayout(channels, n, start, stride);
        }

        /// <summary>
        /// Channel <paramref name="i"/> of every vertex as <paramref name="want"/> floats each (normalised formats
        /// scaled to 0..1 or -1..1); null when there is no such channel or its format is not read here. A vertex past
        /// the end of <paramref name="data"/> stops the reading: it and those after it stay 0.
        /// </summary>
        public float[]? Read(byte[] data, int i, int want)
        {
            if (i >= Channels.Count || Channels[i].Dim == 0 || !Known(Channels[i].Format))
            {
                return null;
            }
            var c = Channels[i];
            int n = Count;
            float[] res = new float[n * want];
            for (int v = 0; v < n; v++)
            {
                int p = Start[c.Stream] + v * Stride[c.Stream] + c.Offset;
                if (p + c.Dim * FormatSize[c.Format] > data.Length)
                {
                    break;
                }
                for (int d = 0; d < Math.Min(want, c.Dim); d++)
                {
                    res[v * want + d] = c.Format switch
                    {
                        0 => BitConverter.ToSingle(data, p + 4 * d),
                        1 => (float)BitConverter.ToHalf(data, p + 2 * d),
                        2 => data[p + d] / 255f,
                        3 => Math.Max((sbyte)data[p + d] / 127f, -1),
                        4 => BitConverter.ToUInt16(data, p + 2 * d) / 65535f,
                        5 => Math.Max(BitConverter.ToInt16(data, p + 2 * d) / 32767f, -1),
                        _ => 0,
                    };
                }
            }
            return res;
        }
    }
}
