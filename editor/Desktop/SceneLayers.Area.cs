using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

public static partial class SceneLayers
{
    /// <summary>
    /// An open area as scene providers read it (IAreaView): its bounds, its objects with the changes not saved yet, and
    /// its ground as the editor has it (heights, biomes, the game's terrain colours and paint), read on a worker. The
    /// changes not saved yet are copied when it is made (on the window's thread); the objects' data and the ground are
    /// read as they are when asked (a brush stroke going on is seen part way, and drawn again once done).
    /// </summary>
    internal sealed class AreaView : IAreaView
    {
        private readonly WorldScene _s;
        private readonly HashSet<int> _deleted;
        private readonly List<Editing.NewObject> _added;
        private readonly Dictionary<string, IReadOnlyList<IObjectView>> _objects = [];

        /// <summary>The area of a scene, as it is now.</summary>
        /// <param name="s">The scene.</param>
        /// <param name="without">Objects left out as if deleted, by id (those a step is about to move).</param>
        public AreaView(WorldScene s, IEnumerable<int>? without = null)
        {
            _s = s;
            var edits = s.Session?.Edits;
            _deleted = edits == null ? [] : [.. edits.Deleted];
            _deleted.UnionWith(without ?? []);
            _added = edits == null ? [] : [.. edits.Added.Where(n => !_deleted.Contains(n.Id))];
        }

        /// <inheritdoc/>
        public float MinX => _s.OriginX;

        /// <inheritdoc/>
        public float MinZ => _s.OriginZ;

        /// <inheritdoc/>
        public float MaxX => _s.OriginX + _s.W - 1;

        /// <inheritdoc/>
        public float MaxZ => _s.OriginZ + _s.H - 1;

        private bool Inside(Vector3 p) => p.X >= MinX && p.X < MaxX && p.Z >= MinZ && p.Z < MaxZ;

        /// <inheritdoc/>
        public IReadOnlyList<IObjectView> ObjectsOf(string prefab)
        {
            lock (_objects)
            {
                if (!_objects.TryGetValue(prefab, out var found))
                {
                    _objects[prefab] = found = Read(StableHash.Of(prefab), prefab);
                }
                return found;
            }
        }

        /// <summary>The objects of a prefab: the world's (not deleted), then those added in this session.</summary>
        private List<IObjectView> Read(int hash, string name)
        {
            var views = new List<IObjectView>();
            var world = _s.World;
            List<(int Id, Vector3 Position, Vector3 Rotation, float Scale)> saved;
            lock (world.Sync)
            {
                saved = [.. world.Objects.Where(o => o.Prefab == hash && !_deleted.Contains(o.Id) && Inside(o.Position)).Select(o => (o.Id, o.Position, o.Rotation, o.Scale.X))
                    .Concat(world.Pieces.Where(p => p.Prefab == hash && !_deleted.Contains(p.Id) && Inside(p.Position)).Select(p => (p.Id, p.Position, p.Rotation, 1f)))];
            }
            var source = world.SourceReader();
            foreach (var (id, position, rotation, scale) in saved)
            {
                views.Add(new ObjectView(name, position, rotation, ZdoData.Parse(world.ObjectBytes(id, source)), scale == 0 ? 1 : scale) { Id = id });
            }
            foreach (var n in _added.Where(n => n.Prefab == hash && Inside(n.Position)))
            {
                if (_s.Session is { } session && ObjectData.Bytes(world, session.Edits, n.Id) is { } bytes)
                {
                    views.Add(new ObjectView(name, n.Position, n.Rotation, ZdoData.Parse(bytes), n.Scale == 0 ? 1 : n.Scale) { Id = n.Id });
                }
            }
            return views;
        }

        /// <summary>The grid point nearest a world point, held to the area.</summary>
        private (int X, int Z) Nearest(float x, float z) =>
            (Math.Clamp((int)MathF.Round(x - _s.OriginX), 0, _s.W - 1), Math.Clamp((int)MathF.Round(z - _s.OriginZ), 0, _s.H - 1));

        /// <inheritdoc/>
        public float GroundHeight(float x, float z)
        {
            float u = Math.Clamp(x - _s.OriginX, 0, _s.W - 1), v = Math.Clamp(z - _s.OriginZ, 0, _s.H - 1);
            int ix = Math.Min((int)u, _s.W - 2), iz = Math.Min((int)v, _s.H - 2);
            float fx = u - ix, fz = v - iz;
            double H(int a, int b) => _s.Heights[(iz + b) * _s.W + ix + a];
            // The game's two triangles a cell: (0,0), (1,0), (0,1), then (1,1), (0,1), (1,0) (Heightmap's collision mesh).
            // Worked out in double and rounded once, as the game's Mono runs a float expression: in float, a point
            // between the grid's could come out a few micrometres off, enough for a mod reading it to round otherwise.
            return fx + fz <= 1
                ? (float)(H(0, 0) + (fx * (H(1, 0) - H(0, 0))) + (fz * (H(0, 1) - H(0, 0))))
                : (float)(H(1, 1) + ((1.0 - fx) * (H(0, 1) - H(1, 1))) + ((1.0 - fz) * (H(1, 0) - H(1, 1))));
        }

        /// <inheritdoc/>
        public int Biome(float x, float z)
        {
            var (gx, gz) = Nearest(x, z);
            return _s.Biomes[gz * _s.W + gx];
        }

        /// <inheritdoc/>
        public bool IsDeepNorth(float x, float z) => _s.Terrain?.Generator.IsDeepnorth(x, z) ?? false;

        /// <inheritdoc/>
        public bool IsAshlands(float x, float z) => _s.Terrain?.Generator.IsAshlands(x, z) ?? false;

        /// <inheritdoc/>
        public uint BiomeColor(int x, int z) => Rgba(_s.BiomeColor, x, z);

        /// <inheritdoc/>
        public uint GroundMask(int x, int z) => Rgba(_s.Mask, x, z);

        /// <summary>Four bytes a grid point (RGBA) as one number, red lowest.</summary>
        private uint Rgba(byte[] bytes, int x, int z)
        {
            var (gx, gz) = Nearest(x, z);
            int i = (gz * _s.W + gx) * 4;
            return (uint)(bytes[i] | bytes[i + 1] << 8 | bytes[i + 2] << 16 | bytes[i + 3] << 24);
        }
    }
}
