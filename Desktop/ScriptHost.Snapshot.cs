using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The area as a script sees it: taken on the window's thread before the script runs.
public static partial class ScriptHost
{
    /// <summary>The area as the script sees it: taken on the window's thread before the script runs.</summary>
    /// <param name="W">Grid points across (x).</param>
    /// <param name="H">Grid points deep (z).</param>
    /// <param name="Ox">World x of the first grid point.</param>
    /// <param name="Oz">World z of the first grid point.</param>
    /// <param name="Heights">The heights now, by grid point.</param>
    /// <param name="Original">The heights as the world generator made them.</param>
    /// <param name="Biomes">The biome of each grid point.</param>
    /// <param name="Mask">The paint, four bytes per grid point.</param>
    /// <param name="Locked">Grid points the script must leave alone.</param>
    /// <param name="Things">The objects standing: index into the scene's things, prefab name, position, kind of object,
    /// whether it is a player-built piece, its rotation (Euler degrees) and scale (zero: the prefab's own).</param>
    /// <param name="Water">The water level.</param>
    /// <param name="Seed">The world's seed (0 without a world).</param>
    /// <param name="World">The world's name.</param>
    /// <param name="Creatable">The prefabs (by hash) the editor can make: the world's own and the game's placeable
    /// kinds and creatures.</param>
    public sealed record Snapshot(int W, int H, float Ox, float Oz, float[] Heights, float[] Original, int[] Biomes, byte[] Mask, bool[] Locked,
        List<(int Index, string Prefab, Vector3 Position, string Kind, bool Piece, Vector3 Rotation, float Scale)> Things, float Water, int Seed, string World, HashSet<int> Creatable)
    {
        /// <summary>Whether the editor can make a prefab (by hash).</summary>
        public bool CanCreate(int prefab) => Creatable.Contains(prefab);
    }

    /// <summary>Takes the snapshot of the session's area, on the window's thread.</summary>
    /// <param name="s">The edit session.</param>
    /// <param name="nameOf">A prefab hash's name (null when unknown).</param>
    public static Snapshot Take(EditSession s, Func<int, string?> nameOf)
    {
        var g = s.Ground;
        var scene = s.Scene;
        float[] original = new float[g.W * g.H];
        bool[] locked = new bool[g.W * g.H];
        for (int p = 0; p < original.Length; p++)
        {
            original[p] = g.Original(p);
            locked[p] = g.Locked(p % g.W, p / g.W);
        }
        List<(int, string, Vector3, string, bool, Vector3, float)> things;
        // Name and kind once per kind of thing, not per thing: an area can hold millions, and a kind is
        // found by matching the name against patterns.
        var known = new Dictionary<(int, bool, bool), (string Name, string Kind)>();
        lock (scene.Things)
        {
            things = new(scene.Things.Count);
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (t.Gone)
                {
                    continue;
                }
                if (!known.TryGetValue((t.Prefab, t.Piece, t.Tamed), out var nk))
                {
                    string? name = nameOf(t.Prefab);
                    nk = known[(t.Prefab, t.Piece, t.Tamed)] = (name ?? "", ObjectKinds.Of(name, t.Piece, t.Tamed).ToString());
                }
                things.Add((i, nk.Name, t.Position, nk.Kind, t.Piece, t.Rotation, t.Scale));
            }
        }
        var world = scene.World;
        // As WorldSave.CanCreate: the world's own objects, the game's placeable kinds and its creatures; without a
        // world (the Workshop), the placeable kinds.
        HashSet<int> creatable = world != null
            ? [.. world.Creatable, .. PrefabCatalog.Creatures]
            : [.. PrefabCatalog.Placeable.Select(p => StableHash.Of(p.Name))];
        return new Snapshot(g.W, g.H, scene.OriginX, scene.OriginZ, (float[])scene.Heights.Clone(), original, (int[])scene.Biomes.Clone(), (byte[])scene.Mask.Clone(), locked,
            things, scene.Water, world?.Seed ?? 0, world?.Name ?? "", creatable);
    }
}
