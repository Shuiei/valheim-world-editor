using System.Linq.Expressions;
using System.Numerics;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// A world laid out by the player's Riverheim (<see cref="RiverheimLibrary"/>), answering as the mod's game side answers
/// the game: the point given as the game's floats widened to double; the height 30 m above Riverheim's (the game's
/// water level), in float; the mask's channels its dirt, snow, paving and the larger of its lava and fertility; the
/// forest factor 2.15 × (1 − its forestation); its biomes, sub-biomes included, as the game's.
/// </summary>
/// <param name="calls">The renderer's calls.</param>
/// <param name="renderer">Riverheim's renderer of the world.</param>
internal sealed class RiverheimGround(RiverheimGround.Calls calls, object renderer) : IGroundLayout
{
    /// <summary>What the mod's game side adds to Riverheim's heights: the game's water level, metres.</summary>
    private const float HeightOffset = 30f;

    /// <summary>The game's forest factor for no forestation (the mod's multiplier).</summary>
    private const double ForestFactor = 2.15;

    /// <inheritdoc/>
    public int Biome(float x, float z) => (int)calls.Biome(calls.BiomeOf(renderer, x, z));

    /// <inheritdoc/>
    public float Height(float x, float z, out Vector4 mask)
    {
        var p = calls.Point(renderer, x, z);
        mask = new Vector4((float)p.Dirt, (float)p.Snow, (float)p.Pave, (float)Math.Max(p.Lava, p.Fertility));
        return HeightOffset + (float)p.Height;
    }

    /// <inheritdoc/>
    public float Forest(float x, float z) => (float)(ForestFactor * (1.0 - calls.Forestation(renderer, x, z)));

    /// <summary>What Riverheim's renderer gives for a point.</summary>
    /// <param name="Height">The ground's height, metres from the water level.</param>
    /// <param name="Dirt">The mask's dirt.</param>
    /// <param name="Snow">The mask's snow.</param>
    /// <param name="Pave">The mask's paving.</param>
    /// <param name="Lava">The lava.</param>
    /// <param name="Fertility">The fertility.</param>
    internal readonly record struct Point(double Height, double Dirt, double Snow, double Pave, double Lava, double Fertility);

    /// <summary>
    /// The renderer's three calls, compiled once for a loaded Riverheim (by name: no reflection per point), and its
    /// biomes as the game's.
    /// </summary>
    internal sealed class Calls
    {
        /// <summary>Riverheim's biomes by name, as the game's (its sub-biomes present themselves as their parents).</summary>
        private static readonly Dictionary<string, Heightmap.Biome> GameBiomes = new(StringComparer.Ordinal)
        {
            ["Meadows"] = Heightmap.Biome.Meadows,
            ["Marsh"] = Heightmap.Biome.Meadows,
            ["Swamp"] = Heightmap.Biome.Swamp,
            ["Mountain"] = Heightmap.Biome.Mountain,
            ["Forest"] = Heightmap.Biome.BlackForest,
            ["ForestLowlands"] = Heightmap.Biome.BlackForest,
            ["Plains"] = Heightmap.Biome.Plains,
            ["Ashlands"] = Heightmap.Biome.AshLands,
            ["Lavaplain"] = Heightmap.Biome.AshLands,
            ["DeepNorth"] = Heightmap.Biome.DeepNorth,
            ["Ocean"] = Heightmap.Biome.Ocean,
            ["Mistlands"] = Heightmap.Biome.Mistlands,
            ["Mistglade"] = Heightmap.Biome.Mistlands,
        };

        /// <summary>The game's biome for each of Riverheim's values.</summary>
        private readonly Dictionary<int, Heightmap.Biome> _biomes = [];

        /// <summary>Compiles the calls on a loaded Riverheim's types.</summary>
        /// <param name="renderer">Its WorldRenderer.</param>
        /// <param name="vector">Its Rfloat2 (a point, two doubles).</param>
        /// <param name="biome">Its Biome enum.</param>
        public Calls(Type renderer, Type vector, Type biome)
        {
            var r = Expression.Parameter(typeof(object), "renderer");
            var x = Expression.Parameter(typeof(double), "x");
            var z = Expression.Parameter(typeof(double), "z");
            var target = Expression.Convert(r, renderer);
            var at = Expression.New(vector.GetConstructor([typeof(double), typeof(double)])!, x, z);
            Expression Call(string name) => Expression.Call(target, renderer.GetMethod(name, [vector])!, at);
            var data = Expression.Variable(renderer.GetMethod("RenderPoint", [vector])!.ReturnType, "data");
            Expression Field(string name) => Expression.Field(data, name);
            var point = Expression.Block(
                [data],
                Expression.Assign(data, Call("RenderPoint")),
                Expression.New(typeof(Point).GetConstructors()[0], Field("height"), Field("dirt"), Field("snow"), Field("pave"), Field("lava"), Field("fertility")));
            Point = Expression.Lambda<Func<object, double, double, Point>>(point, r, x, z).Compile();
            BiomeOf = Expression.Lambda<Func<object, double, double, int>>(Expression.Convert(Call("GetBiome"), typeof(int)), r, x, z).Compile();
            Forestation = Expression.Lambda<Func<object, double, double, double>>(Call("GetForestation"), r, x, z).Compile();
            foreach (object value in Enum.GetValues(biome))
            {
                string name = Enum.GetName(biome, value)!;
                if (!GameBiomes.TryGetValue(name, out var game) && name != "None")
                {
                    Console.WriteLine($"Riverheim: its biome {name} is not one the editor knows: drawn as none.");
                }
                _biomes[Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)] = game;
            }
        }

        /// <summary>The renderer's RenderPoint at a point.</summary>
        public Func<object, double, double, Point> Point { get; }

        /// <summary>The renderer's GetBiome at a point, as Riverheim's value.</summary>
        public Func<object, double, double, int> BiomeOf { get; }

        /// <summary>The renderer's GetForestation at a point.</summary>
        public Func<object, double, double, double> Forestation { get; }

        /// <summary>The game's biome for one of Riverheim's values (none for a value it does not have).</summary>
        public Heightmap.Biome Biome(int value) => _biomes.GetValueOrDefault(value);
    }
}
