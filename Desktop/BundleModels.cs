using System.Collections.Concurrent;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The models of ModelStore read from the game's own files (GameBundles), as the exporter used to copy
/// them: z mirrored (Unity is left-handed, the view right-handed), textures no larger than 1024 pixels,
/// cut-out textures bled at their edges. Ids: a mesh or material is "&lt;bundle&gt;:&lt;path id&gt;", a texture the
/// same with "|cut" for a cut-out one; "+" before them: in the mods' bundles (ModBundles). Mod prefabs, which no
/// bundle holds whole (mods make them while the game runs), are drawn as the game reported it (ModCatalogue.ModelOf):
/// each part's mesh and material found by name in the mods' bundles, else the game's.
/// </summary>
internal sealed class BundleModels : ModelStore.ISource
{
    /// <summary>Textures are drawn no larger than this, in pixels (the mip level at or under it).</summary>
    public const int TextureMax = 1024;

    /// <summary>
    /// Creatures players tame (and their young), drawn in the Tamed animals kind; Deer and Neck are tamed
    /// by server mods.
    /// </summary>
    private static readonly HashSet<string> Tameable = new(StringComparer.Ordinal)
    {
        "Boar", "Boar_piggy", "Wolf", "Wolf_cub", "Lox", "Lox_Calf", "Hen", "Chicken", "Asksvin", "Asksvin_hatchling", "Deer", "Neck",
    };

    /// <summary>The game's files the models are read from.</summary>
    private readonly GameBundles _game;
    /// <summary>The mods' bundles (copied out of their DLLs the first time a mod's model is wanted); null: none.</summary>
    private readonly Lazy<GameBundles?> _mods;
    /// <summary>What marks an id as one in the mods' bundles.</summary>
    private const char Mod = '+';
    /// <summary><see cref="ObjectName"/>'s answers by prefab hash, worked out once each.</summary>
    private readonly ConcurrentDictionary<int, string?> _objectNames = new();

    /// <summary>Models read from these game files; <see cref="For"/> checks they can be read first.</summary>
    /// <param name="game">The game's bundles.</param>
    /// <param name="mods">The mods' bundles, asked for the first time one is needed (null: ModBundles' for the
    /// settings).</param>
    internal BundleModels(GameBundles game, Func<GameBundles?>? mods = null)
    {
        _game = game;
        _mods = new(mods ?? (() => ModBundles.Reader(AppSettings.Load())));
    }

    /// <summary>The models of the game's files (their index read or built now), or null when there are none.</summary>
    public static BundleModels? For(GameBundles? game)
    {
        if (game == null)
        {
            return null;
        }
        try
        {
            return game.Index.Count > 0 ? new BundleModels(game) : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or NotImplementedException)
        {
            Console.WriteLine($"game files: could not read the bundles in {game.Folder}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// World objects with a model of their own: saved (networked) objects that are not build pieces,
    /// creatures, items or ragdolls; also locations (runestones, and the parts of ruins, camps and altars the
    /// game makes again from a location proxy, see LocationParts) and tameable creatures.
    /// </summary>
    /// <returns>The prefab's name, or null when it is not drawn as an object model.</returns>
    public string? ObjectName(int prefab) => _objectNames.GetOrAdd(prefab, p =>
    {
        if (PieceCatalog.Get(p) != null || _game.NameOf(p) is not string name || _game.KindOf(name) is not { } kind)
        {
            return null;
        }
        bool extra = kind.Location || name.StartsWith("Runestone_", StringComparison.Ordinal) || kind.Creature && Tameable.Contains(name);
        return extra || kind.Networked && !kind.Creature && !kind.Item && !name.EndsWith("_ragdoll", StringComparison.Ordinal) ? name : null;
    });

    /// <summary>
    /// A prefab's model, its parts placed in the view's space; dungeon rooms and locations are read as rooms (without
    /// their saved objects, drawn as objects of their own). Null: none.
    /// </summary>
    public ModelStore.Model? Model(string name)
    {
        bool room = Editing.Dungeons.RoomOf(name) != null || _game.KindOf(name) is { Location: true };
        if (_game.LoadModel(name, room) is not { } m)
        {
            return ModCatalogue.ModelOf(name) is { } reported ? ModModel(name, reported) : null;
        }
        var parts = m.Parts.Select(p => new ModelStore.Part(p.Mesh.Key, p.Sub, p.Material?.Key ?? "", ViewMatrix(p.Matrix))).ToList();
        return new ModelStore.Model(name, parts, new Vector3(m.RootScale[0], m.RootScale[1], m.RootScale[2]));
    }

    /// <summary>
    /// A mod prefab's model as the game reported it: each part's mesh found by name (of those so named, the one with
    /// the vertex count the game had, else the first), in the mods' bundles first, and its material by name, in the
    /// mesh's bundle first; parts whose mesh is nowhere (made while the game runs) are left out.
    /// </summary>
    private ModelStore.Model ModModel(string name, ModCatalogue.Model reported)
    {
        var mods = _mods.Value;
        var parts = new List<ModelStore.Part>();
        foreach (var p in reported.Parts)
        {
            if (FindMesh(mods, p) is not { } mesh)
            {
                continue;
            }
            string near = mesh.Id.Contains(':', StringComparison.Ordinal) ? mesh.Id[..mesh.Id.LastIndexOf(':')] : "";
            string material = p.Material is { } mat && FindMaterial(mods, mat, near) is { } found ? found : "";
            parts.Add(new ModelStore.Part(mesh.Id, p.Sub, material, ViewMatrix(p.Matrix)));
        }
        return new ModelStore.Model(name, parts, new Vector3(reported.RootScale[0], reported.RootScale[1], reported.RootScale[2]));
    }

    /// <summary>What marks a mesh id as one of Unity's own primitives, made here (no bundle holds them).</summary>
    private const char Primitive = '~';

    /// <summary>
    /// Unity's own meshes (GameObject.CreatePrimitive, and the built-in meshes a prefab can use), by name, with their
    /// bounds (centre and extent): mods use them, and no bundle holds them.
    /// </summary>
    private static readonly Dictionary<string, float[]> Primitives = new(StringComparer.Ordinal)
    {
        ["Cube"] = [0, 0, 0, 0.5f, 0.5f, 0.5f],
        ["Quad"] = [0, 0, 0, 0.5f, 0.5f, 0],
        ["Plane"] = [0, 0, 0, 5, 0, 5],
    };

    /// <summary>
    /// A part's mesh: of the meshes so named (the mods' bundles first, then the game's), one with the bounds the game
    /// told (when it told them) and its vertex count; else Unity's own primitive of that name and bounds; else, the game
    /// not having told the bounds, the one with the vertex count or the first. Its id; null when there is none.
    /// </summary>
    private (string Id, int Rank)? FindMesh(GameBundles? mods, ModCatalogue.ModelPart p)
    {
        if (p.Bounds is not { } bounds)
        {
            return Find(mods, AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh, p.Vertices);
        }
        var fits = (mods?.Named(AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh) ?? []).Select(w => (Id: Mod + w.Key, Game: mods!, At: w))
            .Concat(_game.Named(AssetsTools.NET.Extra.AssetClassID.Mesh, p.Mesh).Select(w => (Id: w.Key, Game: _game, At: w)))
            .Where(c => c.Game.BoundsOf(c.At) is { } b && SameBounds(b, bounds))
            .Select((c, i) => (c.Id, Rank: i - (c.Game.VertexCountOf(c.At) == p.Vertices ? 100_000 : 0)))
            .ToList();
        if (fits.Count > 0)
        {
            return fits.MinBy(c => c.Rank);
        }
        return Primitives.TryGetValue(p.Mesh, out float[]? own) && SameBounds(own, bounds) ? (Primitive + p.Mesh, 0) : null;
    }

    /// <summary>Whether two bounds (centre and extent) are the same, to a centimetre or a hundredth of their size.</summary>
    private static bool SameBounds(float[] a, float[] b)
    {
        float size = Math.Max(Math.Max(Math.Abs(b[3]), Math.Abs(b[4])), Math.Abs(b[5]));
        float tolerance = Math.Max(0.01f, size * 0.01f);
        for (int i = 0; i < 6; i++)
        {
            if (Math.Abs(a[i] - b[i]) > tolerance)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// One of Unity's own primitives (see <see cref="Primitives"/>) as GameBundles reads meshes: a box or a flat square,
    /// each face with its own vertices, normal and uv (0 to 1), seen from both sides (its material may cull either).
    /// </summary>
    internal static GameBundles.Mesh? PrimitiveMesh(string name)
    {
        if (!Primitives.TryGetValue(name, out float[]? b))
        {
            return null;
        }
        var pos = new List<float>();
        var nor = new List<float>();
        var uv = new List<float>();
        var idx = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v)
        {
            Vector3 c = new Vector3(b[0], b[1], b[2]) + n;
            int first = pos.Count / 3;
            foreach (var (du, dv) in ((float, float)[])[(-1, -1), (1, -1), (1, 1), (-1, 1)])
            {
                Vector3 q = c + du * u + dv * v;
                pos.AddRange([q.X, q.Y, q.Z]);
                var dir = Vector3.Normalize(n == Vector3.Zero ? Vector3.Cross(v, u) : n);
                nor.AddRange([dir.X, dir.Y, dir.Z]);
                uv.AddRange([(du + 1) / 2, (dv + 1) / 2]);
            }
            idx.AddRange([first, first + 1, first + 2, first, first + 2, first + 3, first, first + 2, first + 1, first, first + 3, first + 2]);
        }
        var e = new Vector3(b[3], b[4], b[5]);
        if (e.X > 0 && e.Y > 0 && e.Z > 0)
        {
            Face(new(e.X, 0, 0), new(0, 0, e.Z), new(0, e.Y, 0));
            Face(new(-e.X, 0, 0), new(0, 0, -e.Z), new(0, e.Y, 0));
            Face(new(0, e.Y, 0), new(e.X, 0, 0), new(0, 0, e.Z));
            Face(new(0, -e.Y, 0), new(e.X, 0, 0), new(0, 0, -e.Z));
            Face(new(0, 0, e.Z), new(-e.X, 0, 0), new(0, e.Y, 0));
            Face(new(0, 0, -e.Z), new(e.X, 0, 0), new(0, e.Y, 0));
        }
        else
        {
            // Flat: a quad (x, y) or a plane (x, z).
            Face(Vector3.Zero, new(e.X, 0, 0), e.Y > 0 ? new(0, e.Y, 0) : new(0, 0, e.Z));
        }
        return new GameBundles.Mesh(name, pos.Count / 3, [.. pos], [.. nor], [.. uv], [[.. idx]]);
    }

    /// <summary>
    /// What mods name the game's assets they stand in for until the game runs: Jötunn's placeholders ("JVLmock_"), and
    /// the materials a mod swaps for the game's of the same name ("_REPLACE_").
    /// </summary>
    private static readonly string[] Placeholders = ["JVLmock_", "_REPLACE_"];

    /// <summary>
    /// A material by name (see <see cref="Find"/>), but never a mod's placeholder where the game has the real one: a
    /// placeholder's name (a <see cref="Placeholders"/> prefix) is looked up without its prefix, and a mod's material of
    /// the game's name whose shader is a Jötunn placeholder counts only when the game has none so named.
    /// </summary>
    private string? FindMaterial(GameBundles? mods, string name, string near)
    {
        if (Placeholders.FirstOrDefault(p => name.StartsWith(p, StringComparison.Ordinal)) is { } prefix)
        {
            var real = _game.Named(AssetsTools.NET.Extra.AssetClassID.Material, name[prefix.Length..]);
            return real.Count > 0 ? real[0].Key : Find(mods, AssetsTools.NET.Extra.AssetClassID.Material, name, -1, near)?.Id;
        }
        var named = _game.Named(AssetsTools.NET.Extra.AssetClassID.Material, name);
        var game = named.Count > 0 ? named[0] : null;
        bool Real(GameBundles.Where w) => !(mods!.LoadMaterial(w)?.Shader.StartsWith(Placeholders[0], StringComparison.Ordinal) ?? false);
        var own = game == null ? null : (mods?.Named(AssetsTools.NET.Extra.AssetClassID.Material, name) ?? []).Where(Real).ToList();
        return game != null && own!.Count == 0 ? game.Key : Find(mods, AssetsTools.NET.Extra.AssetClassID.Material, name, -1, near)?.Id;
    }

    /// <summary>
    /// A mesh or material by name: the mods' bundles first, then the game's; of several so named, those in
    /// <paramref name="near"/>'s bundle first, then a mesh with that vertex count. Its id (with <see cref="Mod"/> in the
    /// mods' bundles); null when none has the name.
    /// </summary>
    /// <param name="mods">The mods' bundles, if any.</param>
    /// <param name="kind">Mesh or Material.</param>
    /// <param name="name">The name.</param>
    /// <param name="vertices">A mesh's vertex count (-1: any).</param>
    /// <param name="near">An id's bundle part ("+bundle" or "bundle") whose objects are preferred.</param>
    private (string Id, int Rank)? Find(GameBundles? mods, AssetsTools.NET.Extra.AssetClassID kind, string name, int vertices, string? near = null)
    {
        var all = (mods?.Named(kind, name) ?? []).Select(w => (Id: Mod + w.Key, Game: mods!, At: w))
            .Concat(_game.Named(kind, name).Select(w => (Id: w.Key, Game: _game, At: w))).ToList();
        if (all.Count <= 1)
        {
            return all.Count == 0 ? null : (all[0].Id, 0);
        }
        // Several so named: vertex counts read (the mesh's fields) only then.
        return all.Select((c, i) => (c.Id, Rank: i
                - (near != null && c.Id.StartsWith(near + ":", StringComparison.Ordinal) ? 10_000 : 0)
                - (vertices >= 0 && c.Game.VertexCountOf(c.At) == vertices ? 100_000 : 0)))
            .MinBy(c => c.Rank);
    }

    /// <summary>The bundles an id is in, and the id there (without <see cref="Mod"/>); null for a mod's when there are none.</summary>
    private (GameBundles Game, string Id)? Source(string id) =>
        id.StartsWith(Mod) ? _mods.Value is { } mods ? (mods, id[1..]) : null : (_game, id);

    /// <summary>
    /// Unity's matrix (row-major, column vectors, left-handed) in the view's space (z mirrored on both
    /// sides) as System.Numerics keeps it (row vectors: the transpose).
    /// </summary>
    internal static Matrix4x4 ViewMatrix(float[] u)
    {
        float T(int r, int c) => u[r * 4 + c] * (r == 2 ? -1 : 1) * (c == 2 ? -1 : 1);
        return new Matrix4x4(
            T(0, 0), T(1, 0), T(2, 0), T(3, 0),
            T(0, 1), T(1, 1), T(2, 1), T(3, 1),
            T(0, 2), T(1, 2), T(2, 2), T(3, 2),
            T(0, 3), T(1, 3), T(2, 3), T(3, 3));
    }

    /// <summary>A mesh by its id, in the view's space (see <see cref="ViewMesh"/>); null when it cannot be read.</summary>
    public ModelStore.MeshData? Mesh(string id)
    {
        if (id.StartsWith(Primitive))
        {
            return PrimitiveMesh(id[1..]) is { } own ? ViewMesh(own) : null;
        }
        if (Source(id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadMesh(at) is not { } m)
        {
            return null;
        }
        return ViewMesh(m);
    }

    /// <summary>Interleaved position, normal, uv (8 floats a vertex), z mirrored; indices as Unity has them.</summary>
    /// <remarks>A mesh without normals gets straight up ones; without uv, zeros.</remarks>
    internal static ModelStore.MeshData ViewMesh(GameBundles.Mesh m)
    {
        int n = m.VertexCount;
        float[] v = new float[n * 8];
        for (int i = 0; i < n; i++)
        {
            v[i * 8] = m.Positions[i * 3];
            v[i * 8 + 1] = m.Positions[i * 3 + 1];
            v[i * 8 + 2] = -m.Positions[i * 3 + 2];
            if (m.Normals != null)
            {
                v[i * 8 + 3] = m.Normals[i * 3];
                v[i * 8 + 4] = m.Normals[i * 3 + 1];
                v[i * 8 + 5] = -m.Normals[i * 3 + 2];
            }
            else
            {
                v[i * 8 + 4] = 1;
            }
            if (m.Uv != null)
            {
                v[i * 8 + 6] = m.Uv[i * 2];
                v[i * 8 + 7] = m.Uv[i * 2 + 1];
            }
        }
        uint[][] subs = [.. m.Submeshes.Select(s => s.Select(i => (uint)i).ToArray())];
        return new ModelStore.MeshData(v, subs);
    }

    /// <summary>A material by its id (see <see cref="ViewMaterial"/>); a plain white one when it cannot be read.</summary>
    public ModelStore.MaterialData Material(string id)
    {
        var none = new ModelStore.MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
        if (Source(id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadMaterial(at) is not { } m)
        {
            return none;
        }
        var material = ViewMaterial(m);
        // A mod's textures are in its bundles too.
        return id.StartsWith(Mod) && material.Map != null ? material with { Map = Mod + material.Map } : material;
    }

    /// <summary>
    /// As the exporter judged a material: Unity Standard-style blend modes (_Mode 1: cut out; 2, 3:
    /// transparent, drawn cut out at half), vegetation always cut out and seen from both sides.
    /// </summary>
    internal static ModelStore.MaterialData ViewMaterial(GameBundles.Material m)
    {
        bool vegetation = m.Shader.Contains("Vegetation", StringComparison.Ordinal);
        float mode = m.Floats.GetValueOrDefault("_Mode", 0);
        bool cut = mode == 1 || vegetation || m.Shader.Contains("Grass", StringComparison.Ordinal);
        bool transparent = mode is 2 or 3 || m.Shader.Contains("transparent", StringComparison.OrdinalIgnoreCase);
        float cutoff = cut ? (m.Floats.GetValueOrDefault("_Cutoff", 0.5f) is float c and not 0 ? MathF.Round(c, 3) : 0.5f) : transparent ? 0.5f : 0f;
        bool doubleSided = m.Floats.GetValueOrDefault("_Cull", 2) == 0 || vegetation;
        float[] col = m.Colors.GetValueOrDefault("_Color") ?? [1f, 1f, 1f, 1f];
        var color = new Vector4(ModelStore.Linear(col[0]), ModelStore.Linear(col[1]), ModelStore.Linear(col[2]), col[3]);
        string? map = null;
        var uv = new Vector4(1, 1, 0, 0);
        if (m.Textures.GetValueOrDefault("_MainTex") is { Texture: { } tex } main)
        {
            map = tex.Key + (cut || transparent ? "|cut" : "");
            uv = new Vector4(main.ScaleX, main.ScaleY, main.OffsetX, main.OffsetY);
        }
        return new ModelStore.MaterialData(color, map, cutoff, doubleSided, uv);
    }

    /// <summary>
    /// A texture by its id, RGBA no larger than <see cref="TextureMax"/>; a "|cut" one has its edges bled. Null when
    /// it cannot be read.
    /// </summary>
    public ModelStore.ImageData? Texture(string id)
    {
        bool cut = id.EndsWith("|cut", StringComparison.Ordinal);
        if (Source(cut ? id[..^4] : id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadTexture(at, TextureMax) is not { } t || GameTextures.Rgba(t) is not { } rgba)
        {
            return null;
        }
        if (cut)
        {
            GameTextures.Bleed(rgba, t.Width, t.Height);
        }
        return new ModelStore.ImageData(t.Width, t.Height, rgba);
    }
}
