namespace TerrainEditor.App;

/// <summary>
/// The terrain's shader (Custom/Heightmap, its deferred G-buffer pass) made into the GLSL GameLookGl
/// links with the editor's vertex shader. From the OpenGL core program when the game copy has one
/// (Linux, Mac); else (Valheim for Windows: Direct3D 11 and Vulkan only) from the Vulkan program, SMOL-V
/// compressed and without its uniforms' names: those are put back from its parameter blob, and
/// SPIRV-Cross turns it into GLSL (TerrainShader).
/// </summary>
public static partial class GameShader
{
    /// <summary>Unity's shader platform numbers of the OpenGL core and Vulkan program blobs.</summary>
    public const int OpenGlCore = 15, Vulkan = 18;

    /// <summary>A sub-program of the deferred pass.</summary>
    /// <param name="GpuProgramType">Unity's GPU program type; <see cref="SpirvProgram"/> for Vulkan.</param>
    /// <param name="Keywords">How many shader keywords the variant has.</param>
    /// <param name="BlobIndex">Its program's entry in the platform blob.</param>
    /// <param name="ParameterBlobIndex">Its parameter blob's entry; <c>int.MaxValue</c> when it has none.</param>
    public sealed record SubProgram(int GpuProgramType, int Keywords, int BlobIndex, int ParameterBlobIndex);

    /// <summary>Names read from a parameter blob.</summary>
    /// <param name="Buffers">Per constant buffer name: member offset (bytes) to member name.</param>
    /// <param name="Textures">Texture binding to texture name.</param>
    public sealed record Params(Dictionary<string, Dictionary<uint, string>> Buffers, Dictionary<uint, string> Textures);
}
