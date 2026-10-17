// The terrain shader's Vulkan program: the deferred pass's SPIR-V without keywords, with its names put back (see GameShader).

namespace TerrainEditor.App;

public static partial class GameShader
{
    /// <summary>Unity's GPU program type of a SPIR-V sub-program.</summary>
    public const int SpirvProgram = 25;
    /// <summary>SPIR-V execution models of the fragment and tessellation stages (OpEntryPoint's first operand).</summary>
    private const uint Fragment = 4, TessControl = 1, TessEval = 2;

    /// <summary>
    /// The named SPIR-V (bytes) of the terrain's deferred fragment program, from the Vulkan blob: the first
    /// SPIR-V sub-program with no keywords and no tessellation stages.
    /// </summary>
    /// <exception cref="InvalidDataException">No such sub-program.</exception>
    public static byte[] FromVulkan(byte[] blob, IEnumerable<SubProgram> deferred)
    {
        var entries = Entries(blob);
        foreach (var sp in deferred)
        {
            if (sp.GpuProgramType != SpirvProgram || sp.Keywords != 0 || (uint)sp.BlobIndex >= entries.Count || (uint)sp.ParameterBlobIndex >= entries.Count)
            {
                continue;
            }
            var stages = new Dictionary<uint, uint[]>();
            foreach (var (_, w) in Smolv.Programs(entries[sp.BlobIndex]))
            {
                foreach (var (at, op, a) in Instructions(w))
                {
                    if (op == 15 && Length(w, at) > 0)
                    {
                        stages[w[a]] = w;
                        break;
                    }
                }
            }
            if (!stages.TryGetValue(Fragment, out uint[]? frag) || stages.ContainsKey(TessControl) || stages.ContainsKey(TessEval))
            {
                continue;
            }
            uint[] words = Named(frag, Parameters(entries[sp.ParameterBlobIndex]));
            byte[] bytes = new byte[words.Length * 4];
            Buffer.BlockCopy(words, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        throw new InvalidDataException("The terrain shader has no Vulkan program of the deferred pass without keywords; the editor needs an update.");
    }
}
