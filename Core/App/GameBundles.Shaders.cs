using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

/// <summary>
/// Loading a shader: its programs for each platform.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// A shader's programs.
    /// </summary>
    /// <param name="Platforms">The shader platforms it has (Unity's ShaderCompilerPlatform).</param>
    /// <param name="Blobs">Each platform's program blob, LZ4-unpacked.</param>
    /// <param name="Deferred">The sub-programs of its deferred pass's vertex program (for the Vulkan one).</param>
    public sealed record ShaderPrograms(int[] Platforms, Dictionary<int, byte[]> Blobs, List<GameShader.SubProgram> Deferred);

    /// <summary>
    /// A shader's programs, null when the object is not a Shader. Throws InvalidDataException when a blob does not
    /// unpack, or its parts' offsets and lengths do not fit together or in its data.
    /// </summary>
    public ShaderPrograms? LoadShader(Where at)
    {
        lock (_sync)
        {
            try
            {
                if (Read(at.Bundle, at.PathId) is not { } sh || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Shader)
                {
                    return null;
                }
                int[] platforms = [.. sh["platforms.Array"].Children.Select(c => (int)c.AsUInt)];
                byte[] blob = sh["compressedBlob.Array"].AsByteArray;
                var blobs = new Dictionary<int, byte[]>();
                var offsets = sh["offsets.Array"].Children;
                var compressed = sh["compressedLengths.Array"].Children;
                var decompressed = sh["decompressedLengths.Array"].Children;
                if (offsets.Count < platforms.Length || compressed.Count < platforms.Length || decompressed.Count < platforms.Length)
                {
                    throw new InvalidDataException("a shader names more platforms than it has programs for");
                }
                for (int pi = 0; pi < platforms.Length; pi++)
                {
                    var offs = Numbers(offsets[pi]).ToList();
                    var comp = Numbers(compressed[pi]).ToList();
                    var full = Numbers(decompressed[pi]).ToList();
                    if (comp.Count != offs.Count || full.Count != offs.Count)
                    {
                        throw new InvalidDataException("a shader's program parts do not match their lengths");
                    }
                    using var ms = new MemoryStream();
                    for (int k = 0; k < offs.Count; k++)
                    {
                        // LZ4 unpacks to at most 255 times its size (and a bit): a larger length is not made room for.
                        if ((long)offs[k] + comp[k] > blob.Length || full[k] > (long)comp[k] * 255 + 16)
                        {
                            throw new InvalidDataException("a shader's program part lies outside its data");
                        }
                        byte[] outBuf = new byte[full[k]];
                        int got;
                        try
                        {
                            got = LZ4ps.LZ4Codec.Decode32(blob, (int)offs[k], (int)comp[k], outBuf, 0, (int)full[k], true);
                        }
                        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException)
                        {
                            // LZ4ps's own errors for a broken block or one shorter than said: the shader's error, with
                            // this one in it, is logged where it is handled.
                            throw new InvalidDataException("a shader's program could not be unpacked", e);
                        }
                        if (got != full[k])
                        {
                            throw new InvalidDataException("a shader's program could not be unpacked");
                        }
                        ms.Write(outBuf);
                    }
                    blobs[platforms[pi]] = ms.ToArray();
                }
                var deferred = new List<GameShader.SubProgram>();
                foreach (var sub in sh["m_ParsedForm.m_SubShaders.Array"].Children)
                {
                    foreach (var ps in sub["m_Passes.Array"].Children)
                    {
                        if (ps["m_State.m_Name"].AsString != "DEFERRED")
                        {
                            continue;
                        }
                        var prog = ps["progVertex"];
                        var lists = prog["m_PlayerSubPrograms.Array"].Children;
                        var paramLists = prog["m_ParameterBlobIndices.Array"].Children;
                        for (int li = 0; li < lists.Count; li++)
                        {
                            var items = lists[li]["Array"].Children;
                            var prms = li < paramLists.Count ? Numbers(paramLists[li]).ToList() : [];
                            for (int j = 0; j < items.Count; j++)
                            {
                                var sp = items[j];
                                deferred.Add(new GameShader.SubProgram(sp["m_GpuProgramType"].AsInt, sp["m_KeywordIndices.Array"].Children.Count, (int)sp["m_BlobIndex"].AsUInt, j < prms.Count ? (int)prms[j] : int.MaxValue));
                            }
                        }
                    }
                }
                return new ShaderPrograms(platforms, blobs, deferred);
            }
            finally
            {
                _fields.Clear();
            }
        }
    }
}
