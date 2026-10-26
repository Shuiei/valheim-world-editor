using System.Numerics;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace WorldEditor.Tests;

/// <summary>
/// A made-up world generation mod, as an extension registers it: flat ground at the height its worlds keep in their
/// header. A negative height is a header it refuses; a height over 1000 m a world it refuses to draw; 13 m a height its
/// code cannot answer for (a fault); a live game on 0.x another release it refuses.
/// </summary>
public sealed class FlatlandGenerator : IWorldEditorExtension, IWorldGenerator
{
    /// <summary>Its quick check's refusal of a world too high.</summary>
    public const string TooHigh = "No flat ground that high.";

    /// <summary>Whether its quick check throws (a fault).</summary>
    public bool Faulty { get; set; }

    /// <summary>The mod's version the last live game opened with ran; null when none was told.</summary>
    public string? GameVersion { get; private set; }

    public string Id => "test.flatland";

    public string PluginId => "test.flatland.plugin";

    public string Name => "Flatland";

    public void Register(IExtensionRegistry registry) => registry.AddWorldGenerator(this);

    public GeneratorSettings ReadHeader(IWorldFileReader file, out int fileVersion)
    {
        fileVersion = file.ReadInt();
        float height = file.ReadSingle();
        return height < 0f ? throw new InvalidDataException("A negative height.") : Settings(height);
    }

    public void WriteHeader(IWorldFileWriter file, int fileVersion, GeneratorSettings settings)
    {
        file.WriteInt(fileVersion);
        file.WriteSingle((float)settings.Value);
    }

    public string? Problem(GeneratorSettings settings) => Faulty ? throw new NotSupportedException("broken") : (float)settings.Value > 1000f ? TooHigh : null;

    public IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion)
    {
        GameVersion = gamePluginVersion;
        return gamePluginVersion?.StartsWith("0.", StringComparison.Ordinal) == true
            ? throw new InvalidOperationException($"The game runs Flatland {gamePluginVersion}.")
            : new Ground((float)settings.Value);
    }

    public GeneratorSettings? NewWorldSettings() => Settings(50f);

    public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) => [];

    /// <summary>A world's settings: its height.</summary>
    public static GeneratorSettings Settings(float height) => new($"height {height}", height);

    /// <summary>Its flat ground.</summary>
    private sealed class Ground(float height) : IWorldLayout, IGroundLayout
    {
        public string Version => "2.0.0";

        public bool Slow => false;

        public IGroundLayout LayOut(int seed) => this;

        public int Biome(float x, float z) => (int)Heightmap.Biome.Plains;

        public float Height(float x, float z, out Vector4 mask)
        {
            mask = Vector4.Zero;
            // An unlucky height its code cannot answer for (a fault).
            return Math.Abs(height - 13f) < 0.001f ? throw new ArithmeticException("unlucky") : height;
        }

        public float Forest(float x, float z) => 1f;
    }
}
