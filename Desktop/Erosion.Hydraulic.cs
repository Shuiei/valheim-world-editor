namespace TerrainEditor.Desktop;

/// <summary>
/// Hydraulic erosion, like the web editor's: rain drops run downhill, digging and leaving what they carry.
/// </summary>
public static partial class Erosion
{
    /// <summary>
    /// Water erosion with rain drops (after Hans Theobald Beyer's "Implementation of a method for hydraulic erosion"):
    /// each drop runs downhill for up to 40 steps, digging where it speeds up and leaving what it carries where it
    /// slows down or goes uphill. The rain neither makes nor loses ground: a drop carries exactly what it took, never
    /// digs a point below where it goes next nor fills one above where it is (so pits and spikes can't feed on
    /// themselves), and when it stops, it leaves what it still carries along the way it came, where it can, and gives the
    /// rest back to the points it lowered.
    /// </summary>
    /// <param name="h">Heights of a w × d window (row by row), changed in place.</param>
    /// <param name="wt">How much each point may change (0..1; 0 leaves it alone).</param>
    /// <param name="w">The window's width in points.</param>
    /// <param name="d">Its depth in points.</param>
    /// <param name="drops">How many drops fall, at random points of the window (those on points that may not change are
    /// skipped).</param>
    /// <param name="strength">Scales how much a drop digs (1: the Area tool's).</param>
    /// <param name="rand">Where the drops fall.</param>
    public static void Hydraulic(float[] h, float[] wt, int w, int d, int drops, float strength, Random rand)
    {
        const float Inertia = 0.05f, CapacityK = 4, Deposit = 0.3f, Evaporate = 0.02f, MinSlope = 0.01f;
        const int Life = 40;
        float erode = 0.3f * strength;
        float At(int x, int z) => h[z * w + x];
        (float Gx, float Gz, float H) Gradient(float px, float pz)
        {
            int x = (int)MathF.Floor(px), z = (int)MathF.Floor(pz);
            float u = px - x, v = pz - z;
            float a = At(x, z), b = At(x + 1, z), c = At(x, z + 1), e = At(x + 1, z + 1);
            return ((b - a) * (1 - v) + (e - c) * v, (c - a) * (1 - u) + (e - b) * u, a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + e * u * v);
        }
        // Each point's height when the current drop fell (NaN while the drop hasn't changed it), the points it changed,
        // and where it has been (with its height there).
        float[] was = new float[h.Length];
        Array.Fill(was, float.NaN);
        var changed = new List<int>();
        var path = new List<(float X, float Z, float H)>(Life);
        // Changes the four points round (px, pz) by their shares of amount, each times its weight, never lifting a point
        // above limit nor lowering one below it, and returns the change made in all (less than amount where weights are
        // below 1): what the drop's sediment gives or takes, so no ground is made or lost at soft edges and locked points.
        float Change(float px, float pz, float amount, float limit)
        {
            int x = (int)MathF.Floor(px), z = (int)MathF.Floor(pz);
            float u = px - x, v = pz - z;
            // The four corners round the point, each by its share.
            return Add(z * w + x, (1 - u) * (1 - v)) + Add(z * w + x + 1, u * (1 - v)) + Add((z + 1) * w + x, (1 - u) * v)
                + Add((z + 1) * w + x + 1, u * v);
            float Add(int i, float f)
            {
                float by = amount * f * wt[i];
                by = amount > 0 ? MathF.Min(by, MathF.Max(0, limit - h[i])) : MathF.Max(by, MathF.Min(0, limit - h[i]));
                if (float.IsNaN(was[i]))
                {
                    was[i] = h[i];
                    changed.Add(i);
                }
                h[i] += by;
                return by;
            }
        }
        // A stopped drop leaves what it still carries along the way it came, last place first (as it would where it
        // slows), then gives the rest back to the points it lowered, each in proportion to how far it is below where it
        // was when the drop fell (so none ends above that). There is room for all of it: together they are lower by what
        // the drop carries.
        void Stop(float sediment)
        {
            for (int k = path.Count - 1; k >= 0 && sediment > 0; k--)
            {
                sediment -= Change(path[k].X, path[k].Z, sediment, path[k].H);
            }
            path.Clear();
            float room = 0;
            foreach (int i in changed)
            {
                room += MathF.Max(0, was[i] - h[i]);
            }
            foreach (int i in changed)
            {
                if (sediment > 0 && room > 0)
                {
                    h[i] += sediment * MathF.Max(0, was[i] - h[i]) / room;
                }
                was[i] = float.NaN;
            }
            changed.Clear();
        }
        for (int n = 0; n < drops; n++)
        {
            float px = 1 + (float)rand.NextDouble() * (w - 3), pz = 1 + (float)rand.NextDouble() * (d - 3);
            if (wt[(int)MathF.Floor(pz) * w + (int)MathF.Floor(px)] <= 0)
            {
                continue;
            }
            float dirX = 0, dirZ = 0, speed = 1, water = 1, sediment = 0;
            for (int step = 0; step < Life; step++)
            {
                var (gx, gz, height) = Gradient(px, pz);
                path.Add((px, pz, height));
                dirX = dirX * Inertia - gx * (1 - Inertia);
                dirZ = dirZ * Inertia - gz * (1 - Inertia);
                float len = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
                if (len < 1e-6f)
                {
                    break;
                }
                dirX /= len;
                dirZ /= len;
                float nx = px + dirX, nz = pz + dirZ;
                if (nx < 1 || nz < 1 || nx >= w - 2 || nz >= d - 2)
                {
                    break;
                }
                float next = Gradient(nx, nz).H, dh = next - height;
                float capacity = MathF.Max(-dh, MinSlope) * speed * water * CapacityK;
                if (sediment > capacity || dh > 0)
                {
                    // Uphill or full: drop sediment (enough to fill a pit going uphill).
                    float amount = dh > 0 ? MathF.Min(dh, sediment) : (sediment - capacity) * Deposit;
                    sediment -= Change(px, pz, amount, height);
                }
                else
                {
                    float amount = MathF.Min((capacity - sediment) * erode, -dh);
                    sediment -= Change(px, pz, -amount, next);
                }
                speed = DropSpeed(speed, dh);
                water *= 1 - Evaporate;
                px = nx;
                pz = nz;
            }
            Stop(sediment);
        }
    }

    /// <summary>
    /// A rain drop's speed after a step: going downhill speeds it up, going uphill slows it down (to a stop at most).
    /// </summary>
    /// <param name="speed">Its speed before the step.</param>
    /// <param name="dh">The step's rise: the new height less the old.</param>
    internal static float DropSpeed(float speed, float dh)
    {
        const float Gravity = 4;
        return MathF.Sqrt(MathF.Max(0, speed * speed - dh * Gravity));
    }
}
