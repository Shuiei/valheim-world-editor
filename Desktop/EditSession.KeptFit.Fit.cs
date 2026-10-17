
namespace TerrainEditor.Desktop;

/// <summary>
/// Fitting one kept change to the open area: its points, zones and things, and whether it fits or is kept aside.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Fits the changes of a kept history to the open area (see <see cref="Import"/>).
    /// </summary>
    internal sealed partial class KeptFit
    {
        /// <summary>
        /// The points in this area's grid (-1 for one outside it, or locked), and how many are in.
        /// </summary>
        /// <param name="pts">The points, indices in the change's frame.</param>
        /// <param name="f">The frame the points are in.</param>
        internal (int[] Points, int Inside) Points(int[] pts, Frame f)
        {
            var ground = _session.Ground;
            int[] r = new int[pts.Length];
            int inside = 0;
            for (int n = 0; n < pts.Length; n++)
            {
                int gx = pts[n] % f.W + f.Ox - _ox, gz = pts[n] / f.W + f.Oz - _oz;
                bool inArea = gx >= 0 && gz >= 0 && gx < ground.W && gz < ground.H && !ground.Locked(gx, gz);
                r[n] = inArea ? gz * ground.W + gx : -1;
                inside += inArea ? 1 : 0;
            }
            return (r, inside);
        }

        /// <summary>
        /// Where a kept change stands to this area, and its fitted copy when it fits.
        /// </summary>
        /// <param name="c">The kept change.</param>
        internal (Standing Standing, Change? Fitted) Fit(Change c)
        {
            var f = _k.FrameOf(c);
            var (pts, ptsIn) = Points(c.Points, f);
            var (zones, zonesIn) = Zones(c.Zones, f);
            int[] ids = _k.Ids[c];
            var things = new (int, bool, bool)[ids.Length];
            int thingsIn = 0;
            for (int n = 0; n < ids.Length; n++)
            {
                int? i = Thing(ids[n]);
                things[n] = (i ?? -1, c.Things[n].Before, c.Things[n].After);
                thingsIn += i != null ? 1 : 0;
            }
            int inside = ptsIn + zonesIn + thingsIn, total = pts.Length + zones.Count + ids.Length;
            if (inside < total)
            {
                return (inside == 0 ? Standing.Outside : Standing.Straddles, null);
            }
            var fitted = new Change(c.Label, pts, c.Before, c.After, zones)
            {
                Things = things,
                Resets = c.Resets,
                Time = c.Time,
                Removed = c.Removed,
                Applied = c.Applied,
                Earlier = c.Earlier,
                RevertOf = c.RevertOf != null && _map.TryGetValue(c.RevertOf, out var r) ? r : null,
            };
            _map[c] = fitted;
            return (Standing.Fits, fitted);
        }
    }
}
