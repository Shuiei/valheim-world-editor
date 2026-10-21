namespace WorldEditor.Tests.Unity;

/// <summary>
/// Made-up materials, textures and shaders written into a test bundle.
/// </summary>
public static class TestAssets
{
    /// <summary>
    /// A material: its shader (a reference, or none), texture slots (property, texture reference, tiling, offset),
    /// floats and colours. Returns its path id.
    /// </summary>
    public static long Material(TestBundle b, string name, Fields? shader = null,
        IEnumerable<(string Property, Fields Texture, (double X, double Y) Scale, (double X, double Y) Offset)>? textures = null,
        IEnumerable<(string Name, double Value)>? floats = null, IEnumerable<(string Name, double R, double G, double B, double A)>? colors = null) =>
        b.Assets.Add(UnityClass.Material, UnityTypes.Material, new Fields
        {
            ["m_Name"] = name,
            ["m_Shader"] = shader ?? Fields.Ref(0),
            ["m_SavedProperties"] = new Fields
            {
                ["m_TexEnvs"] = (textures ?? []).Select(t => new Fields
                {
                    ["first"] = t.Property,
                    ["second"] = new Fields { ["m_Texture"] = t.Texture, ["m_Scale"] = new Fields { ["x"] = t.Scale.X, ["y"] = t.Scale.Y }, ["m_Offset"] = new Fields { ["x"] = t.Offset.X, ["y"] = t.Offset.Y } },
                }).ToList(),
                ["m_Floats"] = (floats ?? []).Select(f => new Fields { ["first"] = f.Name, ["second"] = f.Value }).ToList(),
                ["m_Colors"] = (colors ?? []).Select(c => new Fields { ["first"] = c.Name, ["second"] = new Fields { ["r"] = c.R, ["g"] = c.G, ["b"] = c.B, ["a"] = c.A } }).ToList(),
            },
        });

    /// <summary>
    /// A texture of a Unity format with its mip levels' bytes one after the other, in the object or (streamed) in the
    /// bundle's resource file. Returns its path id.
    /// </summary>
    public static long Texture(TestBundle b, string name, int width, int height, int format, int mips, byte[] data, bool streamed = false) =>
        b.Assets.Add(UnityClass.Texture2D, UnityTypes.Texture2D, new Fields
        {
            ["m_Name"] = name,
            ["m_Width"] = width,
            ["m_Height"] = height,
            ["m_TextureFormat"] = format,
            ["m_MipCount"] = mips,
            ["image data"] = streamed ? [] : data,
            ["m_StreamData"] = streamed ? b.StreamData(b.Resource(data), data.Length) : null,
        });

    /// <summary>
    /// A texture array of a graphics format, its slices one after the other (in the object or streamed). Returns its
    /// path id.
    /// </summary>
    public static long TextureArray(TestBundle b, string name, int width, int height, int graphicsFormat, int depth, byte[] data, bool streamed = false) =>
        b.Assets.Add(UnityClass.Texture2DArray, UnityTypes.Texture2DArray, new Fields
        {
            ["m_Name"] = name,
            ["m_Format"] = graphicsFormat,
            ["m_Width"] = width,
            ["m_Height"] = height,
            ["m_Depth"] = depth,
            ["image data"] = streamed ? [] : data,
            ["m_StreamData"] = streamed ? b.StreamData(b.Resource(data), data.Length) : null,
        });

    /// <summary>
    /// A deferred pass sub-program: its blob entry, how many keywords, its GPU program type and its parameter blob entry.
    /// </summary>
    public sealed record SubProgram(uint Blob, int Keywords, int GpuType, uint Parameters);

    /// <summary>
    /// A shader: its parsed name, a pass named <paramref name="pass"/> whose vertex program lists these sub-programs,
    /// and a program blob per platform, each cut into LZ4 parts of <paramref name="part"/> bytes (as Unity packs them).
    /// Returns its path id.
    /// </summary>
    public static long Shader(TestBundle b, string name, IReadOnlyDictionary<int, byte[]> blobs, IEnumerable<SubProgram>? deferred = null, string pass = "DEFERRED", int part = 1 << 16)
    {
        var blob = new List<byte>();
        var offsets = new List<List<uint>>();
        var compressed = new List<List<uint>>();
        var full = new List<List<uint>>();
        foreach (var (_, data) in blobs)
        {
            var o = new List<uint>();
            var c = new List<uint>();
            var f = new List<uint>();
            for (int at = 0; at < data.Length; at += part)
            {
                byte[] packed = UnityFsWriter.Literals(data.AsSpan(at, Math.Min(part, data.Length - at)));
                o.Add((uint)blob.Count);
                c.Add((uint)packed.Length);
                f.Add((uint)Math.Min(part, data.Length - at));
                blob.AddRange(packed);
            }
            offsets.Add(o);
            compressed.Add(c);
            full.Add(f);
        }
        var subs = (deferred ?? []).ToList();
        return b.Assets.Add(UnityClass.Shader, UnityTypes.Shader, new Fields
        {
            ["m_ParsedForm"] = new Fields
            {
                ["m_Name"] = name,
                ["m_SubShaders"] = new List<Fields>
                {
                    new()
                    {
                        ["m_Passes"] = new List<Fields>
                        {
                            new() { ["m_State"] = new Fields { ["m_Name"] = "FORWARD" } },
                            new()
                            {
                                ["m_State"] = new Fields { ["m_Name"] = pass },
                                ["progVertex"] = new Fields
                                {
                                    ["m_PlayerSubPrograms"] = new List<object> { subs.Select(s => new Fields { ["m_BlobIndex"] = s.Blob, ["m_KeywordIndices"] = Enumerable.Repeat((object)(ushort)1, s.Keywords).ToList(), ["m_GpuProgramType"] = s.GpuType }).ToList() },
                                    ["m_ParameterBlobIndices"] = new List<object> { subs.Select(s => (object)s.Parameters).ToList() },
                                },
                            },
                        },
                    },
                },
            },
            ["platforms"] = blobs.Keys.Select(k => (object)(uint)k).ToList(),
            ["offsets"] = offsets,
            ["compressedLengths"] = compressed,
            ["decompressedLengths"] = full,
            ["compressedBlob"] = blob.ToArray(),
        });
    }
}
