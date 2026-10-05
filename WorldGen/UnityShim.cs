using System;
#nullable disable

// Minimal stand-ins for the Unity types the decompiled world generator uses. Arithmetic matches
// Unity's managed implementations (single-precision float math).
namespace ValheimGen;

public struct Vector2
{
	public float x;

	public float y;

	public Vector2(float x, float y)
	{
		this.x = x;
		this.y = y;
	}

	public static Vector2 zero => new(0f, 0f);

	public static Vector2 up => new(0f, 1f);

	public static Vector2 down => new(0f, -1f);

	public static Vector2 left => new(-1f, 0f);

	public static Vector2 right => new(1f, 0f);

	public float magnitude => (float)Math.Sqrt(x * x + y * y);

	public float sqrMagnitude => x * x + y * y;

	public Vector2 normalized
	{
		get
		{
			float m = magnitude;
			return m > 1E-05f ? this / m : zero;
		}
	}

	public static float Distance(Vector2 a, Vector2 b)
	{
		float dx = a.x - b.x;
		float dy = a.y - b.y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}

	public static float SqrMagnitude(Vector2 a) => a.x * a.x + a.y * a.y;

	public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);

	public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);

	public static Vector2 operator -(Vector2 a) => new(0f - a.x, 0f - a.y);

	public static Vector2 operator *(Vector2 a, float d) => new(a.x * d, a.y * d);

	public static Vector2 operator *(float d, Vector2 a) => new(a.x * d, a.y * d);

	public static Vector2 operator /(Vector2 a, float d) => new(a.x / d, a.y / d);

	public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;

	public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

	public override bool Equals(object obj) => obj is Vector2 v && x.Equals(v.x) && y.Equals(v.y);

	public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
}

public struct Vector3
{
	public float x;

	public float y;

	public float z;

	public Vector3(float x, float y, float z)
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public static Vector3 zero => new(0f, 0f, 0f);

	public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);

	public Vector3 normalized => Normalize(this);

	public static Vector3 Normalize(Vector3 v)
	{
		float m = v.magnitude;
		return m > 1E-05f ? v / m : zero;
	}

	public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);

	public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);

	public static Vector3 operator *(Vector3 a, float d) => new(a.x * d, a.y * d, a.z * d);

	public static Vector3 operator /(Vector3 a, float d) => new(a.x / d, a.y / d, a.z / d);
}

public struct Vector2i : IEquatable<Vector2i>
{
	public int x;

	public int y;

	public Vector2i(int x, int y)
	{
		this.x = x;
		this.y = y;
	}

	public bool Equals(Vector2i other) => x == other.x && y == other.y;

	public override bool Equals(object obj) => obj is Vector2i v && Equals(v);

	public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();

	public static bool operator ==(Vector2i a, Vector2i b) => a.Equals(b);

	public static bool operator !=(Vector2i a, Vector2i b) => !a.Equals(b);
}

public struct Vector2s : IEquatable<Vector2s>
{
	public short x;

	public short y;

	public Vector2s(int x, int y)
	{
		this.x = (short)x;
		this.y = (short)y;
	}

	public static Vector2s operator -(Vector2s a, Vector2s b) => new(a.x - b.x, a.y - b.y);

	public bool Equals(Vector2s other) => x == other.x && y == other.y;

	public override bool Equals(object obj) => obj is Vector2s v && Equals(v);

	public override int GetHashCode() => x | (y << 16);
}

public struct Color
{
	public float r;

	public float g;

	public float b;

	public float a;

	public Color(float r, float g, float b, float a)
	{
		this.r = r;
		this.g = g;
		this.b = b;
		this.a = a;
	}

	public static Color black => new(0f, 0f, 0f, 1f);
}

public static class Mathf
{
	public static float Abs(float f) => Math.Abs(f);

	public static float Sin(float f) => (float)Math.Sin(f);

	public static float Cos(float f) => (float)Math.Cos(f);

	public static float Min(float a, float b) => a < b ? a : b;

	public static int Min(int a, int b) => a < b ? a : b;

	public static float Max(float a, float b) => a > b ? a : b;

	public static int Max(int a, int b) => a > b ? a : b;

	public static float Ceil(float f) => (float)Math.Ceiling(f);

	public static int CeilToInt(float f) => (int)Math.Ceiling(f);

	public static int FloorToInt(float f) => (int)Math.Floor(f);

	public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

	public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}

public static class Utils
{
	public static float LerpStep(float l, float h, float v) => Mathf.Clamp((v - l) / (h - l), 0f, 1f);
}

public static class ZLog
{
	public static void Log(object message)
	{
	}
}

public static class Heightmap
{
	public enum Biome
	{
		None = 0,
		Meadows = 1,
		Swamp = 2,
		Mountain = 4,
		BlackForest = 8,
		Plains = 0x10,
		AshLands = 0x20,
		DeepNorth = 0x40,
		Ocean = 0x100,
		Mistlands = 0x200,
		All = 0x37F,
		Land = 0x27F
	}

	public enum BiomeArea
	{
		Edge = 1,
		Median = 2,
		Everything = 3
	}
}

// Biome sectors only drive textures and vegetation, never height; the generator gets the
// "no biome data" answer, the same as for a world without Deep North data.
public sealed class BiomeSector
{
	public Heightmap.Biome Biome;

	public static readonly BiomeSector EmptyBlackForest = new() { Biome = Heightmap.Biome.BlackForest };

	public static readonly BiomeSector EmptyMeadows = new() { Biome = Heightmap.Biome.Meadows };
}

public sealed class AltBiomeWorldData
{
	public bool IsReady => false;

	public BiomeSector[,] PointSectors => null;

	public static int WorldSpaceToMapSpace(float v) => 0;
}

public sealed class World
{
	public int m_seed;

	public string m_seedName = "";

	public int m_worldGenVersion = 2;

	public bool m_menu;

	public AltBiomeWorldData m_biomeData;
}
