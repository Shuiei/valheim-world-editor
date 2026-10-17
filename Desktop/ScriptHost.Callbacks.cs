using Microsoft.CodeAnalysis;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// The callbacks of one run: the script's API calls, bound to the run's copy of the area.
public static partial class ScriptHost
{
    /// <summary>
    /// The callbacks one run gives its script (Height, Set, Paint, Mountain, Objects, Place, Move, Print...). Each one reads
    /// or changes the run's copy of the area, and notes what it changed in the run's changes under their lock.
    /// </summary>
    private sealed partial class Callbacks
    {
        /// <summary>The area as the script sees it, taken before the run.</summary>
        private readonly Snapshot _snap;
        /// <summary>What the run has changed so far.</summary>
        private readonly Changes _ch;
        /// <summary>Stops the script at its next check.</summary>
        private readonly CancellationToken _cancel;
        /// <summary>The heights now, by grid point: the snapshot's, as the script's Set calls changed them.</summary>
        private readonly float[] _heights;
        /// <summary>The noises by seed, made on first use.</summary>
        private readonly Dictionary<int, Noise> _noises = [];
        /// <summary>The lines the script has printed so far.</summary>
        private int _lines;

        /// <summary>The callbacks for a run of a script over this area.</summary>
        public Callbacks(Snapshot snap, Changes ch, CancellationToken cancel)
        {
            _snap = snap;
            _ch = ch;
            _cancel = cancel;
            _heights = (float[])snap.Heights.Clone();
        }

        /// <summary>Objects: every object standing in the area, as the script reads them.</summary>
        public (int, string, float, float, float, string, bool, float, float, float, float)[] Objects() =>
            [.. _snap.Things.Select(t => (t.Index, t.Prefab, t.Position.X, t.Position.Y, t.Position.Z, t.Kind, t.Piece, t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Scale > 0 ? t.Scale : 1))];

        /// <summary>Remove: the object of this index is taken away.</summary>
        public void RemoveThing(int i)
        {
            lock (_ch)
            {
                _ch.Remove.Add(i);
            }
        }

        /// <summary>CanPlace: whether the editor can make a prefab of this name.</summary>
        public bool CanPlace(string name) => _snap.CanCreate(StableHash.Of(name));

        /// <summary>Place: a new object noted, checked as <see cref="Place"/> checks it.</summary>
        public void PlaceObject(string name, float x, float z, float yaw, float scale, float y, float pitch, float roll, bool snapIt)
        {
            lock (_ch)
            {
                Place(_snap, _ch, name, x, z, yaw, scale, y, pitch, roll, snapIt);
            }
        }

        /// <summary>Move: an object's new place, turn and size noted, checked as <see cref="Move"/> checks it.</summary>
        public void MoveObject(int index, float x, float z, float y, float yaw, float pitch, float roll, float scale)
        {
            lock (_ch)
            {
                Move(_snap, _ch, index, x, z, y, yaw, pitch, roll, scale);
            }
        }

        /// <summary>Noise: the seed's fractal noise at a point, scaled to about -1 to 1.</summary>
        public float NoiseAt(float x, float z, int seed)
        {
            lock (_noises)
            {
                if (!_noises.TryGetValue(seed, out var n))
                {
                    n = _noises[seed] = new Noise(seed);
                }
                return n.Fbm(x, z) / 1.6f;
            }
        }

        /// <summary>Print: a line of the script's output (2000 lines at most are kept).</summary>
        public void PrintLine(string text)
        {
            lock (_ch)
            {
                if (_lines++ < 2000)
                {
                    _ch.Output.AppendLine(text);
                }
            }
        }

        /// <summary>Objects put on the ground (their height NaN): the height the ground has as the script leaves it.</summary>
        public void PlaceOnGround()
        {
            for (int i = 0; i < _ch.Add.Count; i++)
            {
                var a = _ch.Add[i];
                if (float.IsNaN(a.Position.Y))
                {
                    _ch.Add[i] = a with { Position = a.Position with { Y = Sample(_heights, a.Position.X, a.Position.Z) } };
                }
            }
        }
    }
}
