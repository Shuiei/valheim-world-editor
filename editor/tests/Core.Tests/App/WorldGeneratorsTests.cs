using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimGen;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// World generation mods' generators an extension registers (WorldGenerators, ModGenerator), with a made-up one: a world
/// file naming it read and written by it, its ground laid out in place of the game's; what it refuses said in its
/// words; a fault turning its extension off and said as the add-on failing; new worlds made with it; the world's Expand
/// World settings as it is told them. Runs alone (the extensions in use are shared).
/// </summary>
[Collection("DataDir")]
public sealed class WorldGeneratorsTests : AppSettingsTestBase, IDisposable
{
    /// <summary>Forgets the extensions in use, so the next test starts without any.</summary>
    public new void Dispose()
    {
        ModExtensions.Forget();
        base.Dispose();
    }

    /// <summary>A made-up world generation mod: flat ground at the height its worlds keep in their header.</summary>
    private sealed class Flatland : IWorldEditorExtension, IWorldGenerator
    {
        /// <summary>Whether its quick check throws (a fault).</summary>
        public bool Faulty { get; set; }

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

        public string? Problem(GeneratorSettings settings) => Faulty ? throw new NotSupportedException("broken") : null;

        public IWorldLayout Open(GeneratorSettings settings, string? gamePluginVersion) => new Ground((float)settings.Value);

        public GeneratorSettings? NewWorldSettings() => Settings(50f);

        public IReadOnlyList<string> Notes(GeneratorSettings settings, ExpandWorldSettings expandWorld) => [];

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

    /// <summary>Puts a Flatland in use and gives its generator.</summary>
    private static ModGenerator Use(Flatland flatland)
    {
        ModExtensions.UseForTests(("Flatland", flatland));
        return Assert.IsType<ModGenerator>(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void AWorldItMadeIsReadAndWrittenByItAndLaidOutWithItsGround()
    {
        var g = Use(new Flatland());
        var rules = new WorldRules { Generator = g.Rules(Flatland.Settings(50f), null) };
        Assert.Equal(("Flatland", "2.0.0", "height 50", false), (rules.Generator.Name, rules.Generator.Version, rules.Generator.Settings, rules.Generator.Slow));
        string folder = Path.Combine(Data, "Flat");
        WorldCreator.Create(folder, "Flat", "abcde", rules: rules);
        var head = WorldSave.ReadMetadata(Path.Combine(folder, "_main.1.fwl2"));
        Assert.Equal(("test.flatland", 50f, "Flat", "abcde"), (head.Generator?.Generator.Id, head.Generator?.Settings.Value, head.Name, head.SeedName));
        var gen = WorldGenerator.Create(new World { Seed = 1, Rules = rules });
        Assert.Equal((Heightmap.Biome.Plains, 50f), (gen.GetBiome(100f, 200f), gen.GetHeight(100f, 200f)));
    }

    [Fact]
    public void WhatItRefusesIsSaidInItsWordsAndAFaultTurnsItsExtensionOff()
    {
        var flatland = new Flatland();
        var g = Use(flatland);
        string folder = Path.Combine(Data, "Sunk");
        WorldCreator.Create(folder, "Sunk", "abcde", rules: new WorldRules { Generator = g.Rules(Flatland.Settings(-1f), null) });
        Assert.Equal("A negative height.", Assert.Throws<GeneratorHeaderException>(() => WorldSave.ReadMetadata(Path.Combine(folder, "_main.1.fwl2"))).Message);
        flatland.Faulty = true;
        Assert.Equal(Lang.T("worldgen.failed", "Flatland"), g.Problem(Flatland.Settings(50f)));
        Assert.Null(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void AFaultAnsweringForAPointIsSaidAndTurnsItsExtensionOff()
    {
        var g = Use(new Flatland());
        var gen = WorldGenerator.Create(new World { Seed = 1, Rules = new WorldRules { Generator = g.Rules(Flatland.Settings(13f), null) } });
        Assert.Equal(Lang.T("worldgen.failed", "Flatland"), Assert.Throws<InvalidOperationException>(() => gen.GetHeight(100f, 200f)).Message);
        Assert.Null(WorldGenerators.Find("test.flatland"));
    }

    [Fact]
    public void NewWorldsAreMadeWithAGeneratorThatMakesThem()
    {
        var settings = new AppSettings();
        ModExtensions.Forget();
        Assert.True(WorldGenerators.ForNewWorld(settings).IsVanilla);
        Use(new Flatland());
        var made = WorldGenerators.ForNewWorld(settings).Generator;
        Assert.Equal(("Flatland", "height 50", 50f), (made?.Name, made?.Settings, made?.Header?.Settings.Value));
    }

    [Fact]
    public void AGeneratorIsToldTheWorldsExpandWorldSettings()
    {
        var plain = WorldGenerators.ExpandWorldOf(WorldRules.Vanilla);
        Assert.Equal((1f, 1f, 10500f, false, false, false), (plain.Stretch, plain.BiomeStretch, plain.Radius, plain.ShapesGround, plain.WorldData, plain.Rivers));
        var ew = WorldGenerators.ExpandWorldOf(new WorldRules { SizeInstalled = true, Radius = 20000f, Stretch = 2f, BiomeStretch = 3f, HeightSeed = 7f, World = [], RiversInstalled = true });
        Assert.Equal((2f, 3f, 20500f, true, true, true), (ew.Stretch, ew.BiomeStretch, ew.Radius, ew.ShapesGround, ew.WorldData, ew.Rivers));
    }
}
