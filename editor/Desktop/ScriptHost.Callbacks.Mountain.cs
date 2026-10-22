using Microsoft.CodeAnalysis;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// The Mountain callback of a run: a mountain from a preset, raised into the ground, with the trees and rocks it buries removed.
public static partial class ScriptHost
{
    /// <summary>The mountain callback: a preset's mountain (rolled, with the given values where they are above zero or not negative) raised into the ground.</summary>
    private sealed partial class Callbacks
    {
        /// <summary>Mountain: a preset's mountain at (x, z), with the given height, radius, roughness and turn where they are set.</summary>
        public void MountainAt(float x, float z, string preset, float height, float radius, float rough, float turn, int seed)
        {
            var pre = Mountain.Presets.FirstOrDefault(m => string.Equals(m.Name, preset, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"no mountain preset “{preset}” ({string.Join(", ", Mountain.Presets.Select(m => m.Name))})");
            var rolled = Mountain.Roll(pre, seed != 0 ? new Random(seed) : new Random());
            var spec = rolled with
            {
                Height = height > 0 ? height : rolled.Height,
                Radius = radius > 0 ? radius : rolled.Radius,
                Rough = rough >= 0 ? rough : rolled.Rough,
                Turn = turn >= 0 ? turn : rolled.Turn,
            };
            var shape = Mountain.Shape(spec);
            RaiseMountain(x, z, spec.Reach, shape);
            RemoveBuried(x, z, shape);
        }

        /// <summary>Raises the ground under the mountain's shape, over the whole-metre offsets within its reach that land in the area.</summary>
        private void RaiseMountain(float x, float z, float reach, Func<float, float, float> shape)
        {
            // Counted, and only where the area is: a float counter stops growing past 2^24 m.
            var (fromZ, toZ) = Offsets(z, reach, _snap.Oz - 1, _snap.Oz + _snap.H);
            var (fromX, toX) = Offsets(x, reach, _snap.Ox - 1, _snap.Ox + _snap.W);
            for (int dz = fromZ; dz <= toZ; dz++)
            {
                _cancel.ThrowIfCancellationRequested();
                for (int dx = fromX; dx <= toX; dx++)
                {
                    float v = shape(dx, dz);
                    if (v != 0 && Point(x + dx, z + dz) is int p and >= 0)
                    {
                        // Read and written under the lock, so two mountains at once both count.
                        lock (_ch)
                        {
                            SetHeight(x + dx, z + dz, _heights[p] + v);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The trees and rocks the mountain buries go, as with the Mountain tool: those on the ground (raised already), not
        /// those high above it, in a dungeon.
        /// </summary>
        private void RemoveBuried(float x, float z, Func<float, float, float> shape)
        {
            foreach (var t in _snap.Things)
            {
                if (!t.Piece && t.Kind is "Trees" or "Rocks" or "Ore" or "Bushes" or "Pickables" && Buried(t.Position, x, z, shape))
                {
                    lock (_ch)
                    {
                        _ch.Remove.Add(t.Index);
                    }
                }
            }
        }

        /// <summary>
        /// Whether the mountain at (x, z) buries a natural object there: where it rises more than Uplift.ClearHeight, the
        /// object standing on the ground.
        /// </summary>
        private bool Buried(System.Numerics.Vector3 at, float x, float z, Func<float, float, float> shape) =>
            shape(at.X - x, at.Z - z) > Uplift.ClearHeight && Uplift.OnTheGround(at.Y, Sample(_heights, at.X, at.Z));
    }
}
