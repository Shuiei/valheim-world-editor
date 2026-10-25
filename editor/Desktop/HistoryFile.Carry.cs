using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>
    /// How read steps are fitted to the world as it is now: their ground values onto the generated ground there now,
    /// and what they did in zones reset since left out.
    /// </summary>
    /// <param name="BaseNow">The generated ground now at a ground point (world metres x, z).</param>
    /// <param name="Reset">The zones reset since the steps were taken (carried across the save or the Apply live that reset them).</param>
    /// <param name="Carried">Carried across a re-read of the open world: the steps keep their own flags (applied, earlier session).</param>
    internal sealed record Fitting(Func<int, int, float> BaseNow, IReadOnlyDictionary<(int X, int Z), ZoneReset> Reset, bool Carried)
    {
        /// <summary>
        /// Puts a step's ground values onto the generated ground as it is now: where it changed (ground discs made or
        /// taken away), the lift takes the difference, so the step still gives the ground it gave.
        /// </summary>
        /// <param name="points">The step's points (world metres).</param>
        /// <param name="bases">The generated ground at them when the step was kept.</param>
        /// <param name="before">The values before the step; changed here.</param>
        /// <param name="after">The values after it; changed here.</param>
        public void Rebase(List<(int X, int Z)> points, float[] bases, Ground.State before, Ground.State after)
        {
            for (int k = 0; k < points.Count; k++)
            {
                // Where it did not change (to well under a millimetre), the values stay as they are.
                float d = bases[k] - BaseNow(points[k].X, points[k].Z);
                if (MathF.Abs(d) >= 1e-5f)
                {
                    before.Lift[k] = EditStore.KeepLift(before.Lift[k] + d);
                    after.Lift[k] = EditStore.KeepLift(after.Lift[k] + d);
                }
            }
        }

        /// <summary>
        /// A step's points and zones without those of zones whose ground was reset since (the zone's ground is the
        /// generated one again; an undo cannot put an older one back there).
        /// </summary>
        /// <param name="points">The step's points (world metres).</param>
        /// <param name="before">The values before the step.</param>
        /// <param name="after">The values after it.</param>
        /// <param name="zones">The zones the step changed (world zone column and row).</param>
        public (List<(int X, int Z)> Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones) LeaveOutReset(
            List<(int X, int Z)> points, Ground.State before, Ground.State after, List<(int X, int Z)> zones)
        {
            bool GroundReset((int X, int Z) zone) => Reset.TryGetValue(zone, out var r) && r.Ground;
            if (!zones.Any(GroundReset))
            {
                return (points, before, after, zones);
            }
            int[] keep = [.. Enumerable.Range(0, points.Count).Where(k => !ZonesAt(points[k].X, points[k].Z).Any(GroundReset))];
            return ([.. keep.Select(k => points[k])], Subset(before, keep), Subset(after, keep), [.. zones.Where(z => !GroundReset(z))]);
        }

        /// <summary>The zones a ground point (world metres) belongs to: two or four on their shared edges.</summary>
        private static IEnumerable<(int X, int Z)> ZonesAt(int x, int z)
        {
            var (zx, zz) = ChunkMath.SectorOf(x, z);
            bool edgeX = x + 32 == zx * 64, edgeZ = z + 32 == zz * 64;
            yield return (zx, zz);
            if (edgeX)
            {
                yield return (zx - 1, zz);
            }
            if (edgeZ)
            {
                yield return (zx, zz - 1);
            }
            if (edgeX && edgeZ)
            {
                yield return (zx - 1, zz - 1);
            }
        }

        /// <summary>Some points' values, in the order given.</summary>
        private static Ground.State Subset(Ground.State s, int[] keep) => new(
            [.. keep.Select(k => s.Level[k])], [.. keep.Select(k => s.Smooth[k])], [.. keep.Select(k => s.Mod[k])],
            [.. keep.SelectMany(k => s.Paint.Skip(k * 4).Take(4))], [.. keep.Select(k => s.PMod[k])], [.. keep.Select(k => s.Lift[k])]);
    }

    /// <summary>
    /// The generated ground at a ground point (world metres x, z), as the world's generator makes it now (with the
    /// world's ground discs), each zone read once; <paramref name="was"/> gives some zones' as they were instead.
    /// </summary>
    internal static Func<int, int, float> BaseHeights(WorldSession w, IReadOnlyDictionary<(int X, int Z), float[]>? was = null)
    {
        var zones = was == null ? [] : new Dictionary<(int X, int Z), float[]>(was);
        return (x, z) =>
        {
            var (zx, zz) = ChunkMath.SectorOf(x, z);
            if (!zones.TryGetValue((zx, zz), out float[]? b))
            {
                zones[(zx, zz)] = b = w.Terrain.BaseZone(zx, zz);
            }
            return b[(z + 32 - zz * 64) * EditStore.Grid + x + 32 - zx * 64];
        };
    }

    /// <summary>
    /// The history as it is now, made ready to go across a re-read of the open world (a save or an Apply live after
    /// which the world, or the ground under the area, is read again): its steps with the generated ground they are on,
    /// and their objects' data read from the world as it is now. Null when there is no step.
    /// </summary>
    /// <param name="w">The world.</param>
    /// <param name="k">The history.</param>
    /// <param name="bases">The generated ground the steps are on, when it changed already (see <see cref="BaseHeights"/>); null: as it is now.</param>
    public static byte[]? Carry(WorldSession w, EditSession.Kept? k, Func<int, int, float>? bases = null)
    {
        if (k == null || k.Undo.Count + k.Redo.Count == 0)
        {
            return null;
        }
        var snapshot = Take(w, k, bases);
        try
        {
            var (things, index) = Find(snapshot.Objects!);
            using var m = new MemoryStream();
            WriteTo(snapshot, things, index, m);
            return m.ToArray();
        }
        finally
        {
            snapshot.Release();
        }
    }

    /// <summary>
    /// The history <see cref="Carry"/> made ready, fitted to the world read again: its objects found again by kind and
    /// place, its ground values onto the generated ground now, what it did in the zones reset meanwhile left out.
    /// </summary>
    /// <param name="w">The world, read again.</param>
    /// <param name="carried">What <see cref="Carry"/> gave.</param>
    /// <param name="reset">The zones reset meanwhile.</param>
    /// <returns>Null when no step is left (or what was carried cannot be read: logged).</returns>
    public static Restored? TakeBack(WorldSession w, byte[] carried, IReadOnlyCollection<ZoneReset> reset)
    {
        try
        {
            using var m = new MemoryStream(carried);
            return ReadFrom(w, m, new Fitting(BaseHeights(w), reset.ToDictionary(r => (r.X, r.Z)), Carried: true));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException)
        {
            Log.Caught(ex, "fitting the history to the world read again (it starts over)");
            return null;
        }
    }
}
