namespace TerrainEditor.Desktop;

/// <summary>
/// Erosion, like the web editor's (editor/erosion.js; VoxelSniper's erode brush, Axiom's erosion): ground that weathers
/// like real ground. Thermal: where the ground is steeper than its resting angle, material slides down to its lower
/// neighbours (screes, softened cliffs). Water: drops of rain run downhill, dig where they speed up and leave what they
/// carry where they slow down (gullies, fans, smooth valleys). Both work on a local copy of the heights, written back
/// through SetHeight (so the ±8 m limit applies).
/// </summary>
public static class Erosion
{
    /// <summary>
    /// The eight neighbours of a grid point.
    /// </summary>
    private static readonly (int Dx, int Dz)[] N8 = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// Thermal erosion: where the ground is steeper than its resting angle, material slides down to its lower
    /// neighbours. The window's edge points only take material, they never give it.
    /// </summary>
    /// <param name="h">Heights of a w × d window (row by row), changed in place.</param>
    /// <param name="wt">How much each point may change (0..1; 0 leaves it alone).</param>
    /// <param name="w">The window's width in points.</param>
    /// <param name="d">Its depth in points.</param>
    /// <param name="talus">Steepest slope (rise per metre) that stays put; see Talus.</param>
    /// <param name="amount">Share of the excess moved per pass.</param>
    /// <param name="passes">How many times it runs over the window.</param>
    public static void Thermal(float[] h, float[] wt, int w, int d, float talus, float amount, int passes)
    {
        float[] delta = new float[h.Length];
        var ds = new List<(int J, float Diff)>(8);
        for (int p = 0; p < passes; p++)
        {
            Array.Clear(delta);
            for (int z = 1; z < d - 1; z++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int i = z * w + x;
                    float k = wt[i];
                    if (k <= 0)
                    {
                        continue;
                    }
                    float total = 0, maxD = 0;
                    ds.Clear();
                    foreach (var (dx, dz) in N8)
                    {
                        int j = (z + dz) * w + x + dx;
                        float dist = dx != 0 && dz != 0 ? MathF.Sqrt(2) : 1, diff = h[i] - h[j] - talus * dist;
                        if (diff > 0 && wt[j] > 0)
                        {
                            ds.Add((j, diff));
                            total += diff;
                            maxD = MathF.Max(maxD, diff);
                        }
                    }
                    if (total == 0)
                    {
                        continue;
                    }
                    // Move half of the largest excess (so the slope settles instead of swinging), shared out.
                    float move = maxD * 0.5f * amount * k;
                    delta[i] -= move;
                    foreach (var (j, diff) in ds)
                    {
                        delta[j] += move * diff / total;
                    }
                }
            }
            for (int i = 0; i < h.Length; i++)
            {
                h[i] += delta[i];
            }
        }
    }

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

    /// <summary>
    /// Copies the heights of a window of the ground, runs fn on them, and writes back what changed through SetHeight
    /// (so the ±8 m limit applies).
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="x0">The window's first grid x (clamped to the grid).</param>
    /// <param name="z0">Its first grid z.</param>
    /// <param name="x1">Its last grid x (included).</param>
    /// <param name="z1">Its last grid z (included).</param>
    /// <param name="weightOf">How much the point at (grid x, grid z, index) may change, 0..1; locked points get
    /// 0.</param>
    /// <param name="fn">The erosion: heights, weights, width, depth (as Thermal and Hydraulic take them).</param>
    /// <param name="touched">Gets the indices of the points changed.</param>
    /// <param name="clamped">Set when a point reached the ±8 m limit; left as it was otherwise.</param>
    /// <returns>The window, clamped to the grid; nothing runs when it is less than 3 points wide or deep.</returns>
    public static (int X0, int Z0, int X1, int Z1) Run(Ground g, int x0, int z0, int x1, int z1, Func<int, int, int, float> weightOf,
        Action<float[], float[], int, int> fn, ICollection<int> touched, ref bool clamped)
    {
        x0 = Math.Max(0, x0);
        z0 = Math.Max(0, z0);
        x1 = Math.Min(g.W - 1, x1);
        z1 = Math.Min(g.H - 1, z1);
        int w = x1 - x0 + 1, d = z1 - z0 + 1;
        if (w < 3 || d < 3)
        {
            return (x0, z0, x1, z1);
        }
        float[] h = new float[w * d];
        float[] wt = new float[w * d];
        for (int z = 0; z < d; z++)
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x0 + x, gz = z0 + z, p = gz * g.W + gx;
                h[z * w + x] = g.HeightOf(p);
                wt[z * w + x] = g.Locked(gx, gz) ? 0 : weightOf(gx, gz, p);
            }
        }
        float[] before = (float[])h.Clone();
        fn(h, wt, w, d);
        for (int i = 0; i < h.Length; i++)
        {
            if (MathF.Abs(h[i] - before[i]) < 1e-5f)
            {
                continue;
            }
            int p = (z0 + i / w) * g.W + x0 + i % w;
            clamped |= g.SetHeight(p, h[i]);
            touched.Add(p);
        }
        return (x0, z0, x1, z1);
    }

    /// <summary>
    /// A resting angle in degrees as the slope Thermal takes (rise per metre).
    /// </summary>
    public static float Talus(float degrees) => MathF.Tan(degrees * MathF.PI / 180);

    /// <summary>
    /// The Area tool's Erode: both kinds over the whole selection, in one go (thermal, rain, then thermal again to
    /// settle).
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="a">The selection and how much each of its points may change.</param>
    /// <param name="restAngle">The resting angle in degrees.</param>
    /// <param name="rand">Where the drops fall.</param>
    /// <param name="touched">Gets the indices of the points changed.</param>
    /// <returns>
    /// The grid rectangle worked on (the selection and one point around it), and whether a point reached the game's
    /// ±8 m limit.
    /// </returns>
    public static ((int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Area(Ground g, AreaTool.Weights a, float restAngle, Random rand, ICollection<int> touched)
    {
        var weights = a.Cells.ToDictionary(c => c.G, c => c.W);
        bool clamped = false;
        float talus = Talus(restAngle);
        var rect = Run(g, a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1, (_, _, p) => weights.GetValueOrDefault(p), (h, wt, w, d) =>
        {
            Thermal(h, wt, w, d, talus, 0.8f, 12);
            Hydraulic(h, wt, w, d, (int)MathF.Ceiling(w * d * 0.6f), 1, rand);
            Thermal(h, wt, w, d, talus, 0.5f, 4);
        }, touched, ref clamped);
        return (rect, clamped);
    }
}
