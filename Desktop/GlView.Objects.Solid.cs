using System.Numerics;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Models drawn solid at places that are not things of the scene: their instances made from the places, and where an object goes.
public sealed partial class GlView
{
    /// <summary>
    /// The instances of models drawn solid at places that are not things of the scene (dungeon rooms, locations'
    /// parts), on the graphics card: made from one list and kept while that list and its kinds' models stay the same,
    /// so a frame only draws them.
    /// </summary>
    private sealed class Solid
    {
        /// <summary>The list they were made from (compared by reference: the caches give the same list until it changes).</summary>
        public object? Source;
        /// <summary>The area they were made for (its middle places them).</summary>
        public WorldScene? Scene;
        /// <summary>Each kind in the list: its group then, and whether it was drawn (its model read).</summary>
        public readonly List<(int Prefab, bool Piece, Group Group, bool Drawn)> Kinds = [];
        /// <summary>
        /// One draw per model part of each kind drawn: its batch, the buffer of its instances, how many, and their runs by
        /// area cell (<see cref="Runs"/>, in the list's order).
        /// </summary>
        public readonly List<(Batch Batch, uint Vbo, int Count, (int Cell, int Start, int Count)[] Runs)> Draws = [];
        /// <summary>The cells (<c>_cellsMade</c>) whose boxes were grown by these instances' boxes; -1 for none.</summary>
        public int Cells = -1;
        /// <summary>The buffers made so far, used again when the instances are made again.</summary>
        public readonly List<uint> Buffers = [];

        /// <summary>Forgets everything (the graphics context went, and its buffers with it).</summary>
        public void Forget()
        {
            Source = null;
            Scene = null;
            Cells = -1;
            Kinds.Clear();
            Draws.Clear();
            Buffers.Clear();
        }
    }

    /// <summary>The dungeon rooms' instances (DrawRooms).</summary>
    private readonly Solid _roomsMade = new();
    /// <summary>The locations' parts' instances (DrawLocations).</summary>
    private readonly Solid _locationsMade = new();

    /// <summary>
    /// Makes the instances of <c>DrawSolid</c> again: one buffer of matrices per model part of each kind whose model is
    /// read, the buffers made before used again. Each instance's placed model box grows the box of the area cell it
    /// stands in, so the cell shows whenever any of it is in view.
    /// </summary>
    private unsafe void MakeSolid(WorldScene s, object source, IEnumerable<(int Prefab, Vector3 Position, Vector3 Rotation)> shown, Solid made)
    {
        made.Source = source;
        made.Scene = s;
        made.Cells = _cellsMade;
        made.Kinds.Clear();
        made.Draws.Clear();
        int used = 0;
        foreach (var byKind in shown.GroupBy(o => o.Prefab))
        {
            int prefab = byKind.Key;
            var (piece, g) = PieceAndGroup(prefab);
            bool drawn = DrawnSolid(g);
            made.Kinds.Add((prefab, piece, g, drawn));
            if (!drawn)
            {
                continue;
            }
            var placements = byKind.Select(o => Placement(s, new WorldScene.Thing(0, prefab, o.Position, o.Rotation, 0f, piece), g.RootScale)).ToList();
            (Vector3 Min, Vector3 Max)? box;
            lock (_groups)
            {
                box = g.Box;
            }
            int[] cells = new int[placements.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = CellOf(s, placements[i].Translation);
                Grow(_cellBounds, cells[i], PlacedBox(box, placements[i]));
            }
            var runs = Runs(cells);
            var data = new Matrix4x4[placements.Count];
            foreach (var (b, pre) in g.Batches)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = pre * placements[i];
                }
                if (used == made.Buffers.Count)
                {
                    made.Buffers.Add(_own.Buffer());
                }
                uint vbo = made.Buffers[used++];
                _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
                fixed (Matrix4x4* p = data)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), p, BufferUsageARB.StaticDraw);
                }
                made.Draws.Add((b, vbo, data.Length, runs));
            }
        }
    }

    /// <summary>
    /// The rooms of the area's dungeons (not objects of the save: entries in their dungeon's data, see
    /// DungeonRooms), drawn like objects; a room shown elsewhere by the Dungeon tool (moving, adding) too.
    /// </summary>
    private void DrawRooms(WorldScene s)
    {
        var dungeons = DungeonRooms.Shown(s, Wake);
        DrawSolid(s, dungeons, () => dungeons.SelectMany(d => d.Rooms).Select(r => (r.Hash, r.Position, BlueprintFormats.ToEuler(r.Rotation))), _roomsMade);
    }

    /// <summary>
    /// The parts of the area's locations that are not saved objects (see LocationParts), drawn like objects with the
    /// Ruins &amp; structures kind; they cannot be picked.
    /// </summary>
    private void DrawLocations(WorldScene s)
    {
        var shown = LocationParts.Of(s);
        DrawSolid(s, shown, () => shown.Select(l => (l.Location, l.Position, l.Rotation)), _locationsMade);
    }

    /// <summary>
    /// Where an object goes, in view space: Unity's rotation (Euler degrees, applied z, x, then y)
    /// mirrored in z, like the web editor's objects.js.
    /// </summary>
    private static Matrix4x4 Placement(WorldScene s, WorldScene.Thing t, Vector3 rootScale)
    {
        const float D = MathF.PI / 180f;
        var q = Quaternion.CreateFromYawPitchRoll(t.Rotation.Y * D, t.Rotation.X * D, t.Rotation.Z * D);
        q = new Quaternion(-q.X, -q.Y, q.Z, q.W);
        var scale = t.Scale > 0 ? new Vector3(t.Scale) : rootScale;
        var pos = ToView(s, t.Position);
        return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(pos);
    }
}
