// The editor's API for scripts (the Script tool), compiled together with each script. Everything is in
// world metres (x east, z north); heights are the ground's height above the world's zero (the sea is at
// Area.Water). Changes are kept until the script ends, then go into the area as one undo step.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

// Filled by the editor before the script runs.
public static class __Host
{
	public static Func<float, float, float> Height = null!, Original = null!;
	public static Action<float, float, float> Set = null!;
	public static Func<float, float, string> Biome = null!;
	public static Action<float, float, string, float> Paint = null!;
	public static Action<float, float, string, float, float, float, float, int> Mountain = null!;
	public static Func<(int Id, string Prefab, float X, float Y, float Z, string Kind, bool Building)[]> Objects = null!;
	public static Action<int> Remove = null!;
	public static Action<string, float, float, float, float, float> Place = null!;
	public static Func<string, bool> CanPlace = null!;
	public static Func<float, float, int, float> Noise = null!;
	public static Action<string> Print = null!;
	public static Action Check = null!;
	public static bool NoLimit = true;
	public static float MinX, MinZ, MaxX, MaxZ, Water;
	public static int Seed;
	public static string World = "";
}

// The open area: its corners, middle, the sea level, the world's seed.
public static class Area
{
	public static float MinX => __Host.MinX;
	public static float MinZ => __Host.MinZ;
	public static float MaxX => __Host.MaxX;
	public static float MaxZ => __Host.MaxZ;
	public static float CenterX => (__Host.MinX + __Host.MaxX) / 2;
	public static float CenterZ => (__Host.MinZ + __Host.MaxZ) / 2;
	public static float Water => __Host.Water;
	public static int WorldSeed => __Host.Seed;
	public static string World => __Host.World;

	// Every ground point of the area (one per metre), as (x, z), for loops over the whole area.
	public static IEnumerable<(float X, float Z)> Points(float step = 1)
	{
		for (float z = MinZ + 1; z < MaxZ; z += step)
		{
			__Host.Check();
			for (float x = MinX + 1; x < MaxX; x += step)
			{
				yield return (x, z);
			}
		}
	}

	public static bool Inside(float x, float z) => x > MinX && z > MinZ && x < MaxX && z < MaxZ;
}

// The ground: one point per metre. Past the game's ±8 m unless NoLimit is false (saving turns that
// ground into ground discs the game counts as generated ground).
public static class Ground
{
	// On by default for scripts; off keeps every change within the game's ±8 m of the original ground.
	public static bool NoLimit { get => __Host.NoLimit; set => __Host.NoLimit = value; }

	// The ground's height now (with the script's changes so far).
	public static float Height(float x, float z) => __Host.Height(x, z);

	// The ground before any edit: the generated ground (with ground discs and lifts).
	public static float Original(float x, float z) => __Host.Original(x, z);

	public static void Set(float x, float z, float height) => __Host.Set(x, z, height);

	public static void Raise(float x, float z, float metres) => __Host.Set(x, z, __Host.Height(x, z) + metres);

	public static void Lower(float x, float z, float metres) => Raise(x, z, -metres);

	// Meadows, BlackForest, Swamp, Mountain, Plains, Ocean, Mistlands, AshLands, DeepNorth.
	public static string Biome(float x, float z) => __Host.Biome(x, z);

	// Paint: "dirt", "cultivated", "paved" or "clear" (back to the biome's own), strength 0..1.
	public static void Paint(float x, float z, string kind, float strength = 1) => __Host.Paint(x, z, kind, strength);

	// Adds metres(dx, dz) to the ground within radius of (x, z) (dx, dz: metres from the middle).
	public static void Shape(float x, float z, float radius, Func<float, float, float> metres)
	{
		for (float dz = -MathF.Ceiling(radius); dz <= radius; dz++)
		{
			__Host.Check();
			for (float dx = -MathF.Ceiling(radius); dx <= radius; dx++)
			{
				if (dx * dx + dz * dz <= radius * radius && Area.Inside(x + dx, z + dz))
				{
					float m = metres(dx, dz);
					if (m != 0)
					{
						Raise(x + dx, z + dz, m);
					}
				}
			}
		}
	}

	// A mountain of the Mountain tool: preset "Lone peak", "Ridge", "Mountain range", "Mesa", "Volcano"
	// or "Rolling hills"; 0 (or less) for a value takes a random one of the preset (from seed).
	public static void Mountain(float x, float z, string preset = "Lone peak", float height = 0, float radius = 0, float rough = -1, float turn = -1, int seed = 0) =>
		__Host.Mountain(x, z, preset, height, radius, rough, turn, seed);
}

// A thing in the area: a tree, rock, building piece...
public sealed record Obj(int Id, string Prefab, float X, float Y, float Z, string Kind, bool Building);

public static class Objects
{
	private static List<Obj>? _all;

	// Everything in the area (as it was when the script started).
	public static IReadOnlyList<Obj> All => _all ??= __Host.Objects().Select(o => new Obj(o.Id, o.Prefab, o.X, o.Y, o.Z, o.Kind, o.Building)).ToList();

	// Kinds: Buildings, Ruins, Trees, Rocks, Ore, Bushes, Pickables, Animals, Runestones, Other.
	public static IEnumerable<Obj> OfKind(string kind) => All.Where(o => string.Equals(o.Kind, kind, StringComparison.OrdinalIgnoreCase));

	public static IEnumerable<Obj> Near(float x, float z, float radius) => All.Where(o => (o.X - x) * (o.X - x) + (o.Z - z) * (o.Z - z) <= radius * radius);

	public static void Remove(Obj o) => __Host.Remove(o.Id);

	// A new object of the game's (its prefab name: "Beech1", "rock4_forest", "piece_chest_wood"...).
	// y: its height (by default on the ground there, as the ground is when the script ends).
	// yaw: its turn in degrees; scale: its size (0 keeps the prefab's own).
	public static void Place(string prefab, float x, float z, float yaw = 0, float scale = 0, float y = float.NaN) =>
		__Host.Place(prefab, x, z, yaw, scale, y);

	// Whether the editor can make objects of this prefab.
	public static bool CanPlace(string prefab) => __Host.CanPlace(prefab);
}

public static class Noise
{
	// Smooth natural noise, about -1..1; scale is the size of its bumps in metres.
	public static float At(float x, float z, float scale = 32, int seed = 0) => __Host.Noise(x / scale, z / scale, seed);
}

// Random numbers from a seed (Rnd.Seed = 42 for the same results each run).
public static class Rnd
{
	private static Random _r = new();

	public static int Seed { set => _r = new Random(value); }

	public static float Next() => (float)_r.NextDouble();

	public static float Range(float min, float max) => min + (float)_r.NextDouble() * (max - min);

	public static int Int(int min, int maxExclusive) => _r.Next(min, maxExclusive);

	public static bool Chance(float p) => _r.NextDouble() < p;

	public static T Pick<T>(params T[] items) => items[_r.Next(items.Length)];
}

public static class Globals
{
	// A line in the Script tool's output.
	public static void Print(object? o) => __Host.Print(o?.ToString() ?? "null");
}
