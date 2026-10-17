
namespace TerrainEditor.Desktop;

/// <summary>
/// The kept history fitted to this area, change by change: each change's points, zones and things are read against the
/// area, the things other areas hold that a change needs are found, and what does not fit is kept aside.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Fits the changes of a kept history to the open area (see <see cref="Import"/>).
    /// </summary>
    internal sealed partial class KeptFit
    {
        /// <summary>The session fitted into.</summary>
        private readonly EditSession _session;
        /// <summary>The kept history.</summary>
        private readonly Kept _k;
        /// <summary>The indices of the area's things by object id (the first of each).</summary>
        private readonly Dictionary<int, int> _index;
        /// <summary>Things of the world found by id, which this area does not have.</summary>
        private readonly Dictionary<int, WorldScene.Thing> _found;
        /// <summary>The area's world position of its grid's first point, in metres (x and z).</summary>
        private readonly int _ox, _oz;
        /// <summary>The area's extent in metres: a thing inside it has its position in these bounds.</summary>
        private readonly float _minX, _maxX, _minZ, _maxZ;
        /// <summary>The fitted copy of each change that fits.</summary>
        private readonly Dictionary<Change, Change> _map = [];

        /// <summary>
        /// The things found elsewhere that a fitted change refers to, in the order they were met: they join the scene
        /// when the change is kept here.
        /// </summary>
        public List<WorldScene.Thing> Pending { get; } = [];

        /// <summary>The scene's thing count before the import: things found beyond it get indices from here on.</summary>
        public int First { get; }

        /// <summary>
        /// A fitting of one kept history to the session's open area.
        /// </summary>
        /// <param name="session">The session the history is imported into.</param>
        /// <param name="k">The kept history.</param>
        /// <param name="index">The area's things by object id.</param>
        /// <param name="found">Things of the world found by id.</param>
        public KeptFit(EditSession session, Kept k, Dictionary<int, int> index, Dictionary<int, WorldScene.Thing> found)
        {
            _session = session;
            _k = k;
            _index = index;
            _found = found;
            var scene = session.Scene;
            _ox = scene.X0 * 64 - 32;
            _oz = scene.Z0 * 64 - 32;
            _minX = scene.OriginX;
            _maxX = (scene.X0 + scene.ZonesX - 1) * 64f + 32f;
            _minZ = scene.OriginZ;
            _maxZ = (scene.Z0 + scene.ZonesZ - 1) * 64f + 32f;
            First = scene.Things.Count;
        }

        /// <summary>
        /// The thing index of an object id in the area, or of a thing found elsewhere that lies inside the area (it is
        /// then pending); null when neither.
        /// </summary>
        /// <param name="id">The object id.</param>
        internal int? Thing(int id)
        {
            if (_index.TryGetValue(id, out int i))
            {
                return i;
            }
            if (_found.TryGetValue(id, out var t) && t.Position.X >= _minX && t.Position.X < _maxX && t.Position.Z >= _minZ && t.Position.Z < _maxZ)
            {
                Pending.Add(t);
                _index[id] = First + Pending.Count - 1;
                return _index[id];
            }
            return null;
        }

        /// <summary>
        /// The zones relative to this area, and how many are in it.
        /// </summary>
        /// <param name="zones">The zones in the change's frame.</param>
        /// <param name="f">The frame the zones are in.</param>
        internal (List<(int X, int Z)> Zones, int Inside) Zones(List<(int X, int Z)> zones, Frame f)
        {
            var scene = _session.Scene;
            var r = zones.Select(z => (X: z.X + f.X0 - scene.X0, Z: z.Z + f.Z0 - scene.Z0)).ToList();
            return (r, r.Count(z => z.X >= 0 && z.Z >= 0 && z.X < scene.ZonesX && z.Z < scene.ZonesZ));
        }

        /// <summary>
        /// A change kept aside: the change, its frame and its objects' ids, as the history was kept.
        /// </summary>
        /// <param name="c">The kept change.</param>
        internal (Change, Frame, int[]) Aside(Change c) => (c, _k.FrameOf(c), _k.Ids[c]);
    }
}
