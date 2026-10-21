namespace TerrainEditor.Editing;

internal sealed partial class DungeonDresser
{
    internal sealed partial class Frame
    {
        /// <summary>Whether a circle of radius r at (u, v) is inside the walls (over the room's own tiles) and clear of what is taken.</summary>
        public bool Free(float u, float v, float r)
        {
            // Within the walls' inner faces of the box its tiles reach (its own, and out over any recess).
            float thick = Across - HalfA;
            if (u < 2 * A0 - Across + thick + r - 0.15f || u > 2 * (A1 + 1) - Across - thick - r + 0.15f
                || v < 2 * B0 - Deep + thick + r - 0.15f || v > 2 * (B1 + 1) - Deep - thick - r + 0.15f)
            {
                return false;
            }
            if (S.Shape != DungeonPlan.Shape.Rect)
            {
                float in_ = Math.Max(0, r - 0.15f);
                if (!Inside(u, v) || !Inside(u - in_, v) || !Inside(u + in_, v) || !Inside(u, v - in_) || !Inside(u, v + in_))
                {
                    return false;
                }
            }
            var p = W(u, v, 0);
            return Taken.All(t => (t.X - p.X) * (t.X - p.X) + (t.Z - p.Z) * (t.Z - p.Z) >= (t.R + r) * (t.R + r));
        }

        /// <summary>Keeps a circle of radius r at (u, v) for something.</summary>
        public void Take(float u, float v, float r)
        {
            var p = W(u, v, 0);
            Taken.Add((p.X, p.Z, r));
        }

        /// <summary>Whether a point on a wall is in front of a doorway (within clear metres of its middle).</summary>
        public bool NearDoor(float u, float v, float clear)
        {
            var p = W(u, v, 0);
            return Mouths.Any(m => (m.X - p.X) * (m.X - p.X) + (m.Z - p.Z) * (m.Z - p.Z) < clear * clear);
        }

        /// <summary>
        /// Spots along the walls (on their inner face), every step metres, facing in (local yaw), not by
        /// doorways.
        /// </summary>
        public List<(float U, float V, float Yaw)> WallSpots(float step, float clear = 3f)
        {
            var spots = new List<(float, float, float)>();
            foreach (var (u, v, du, dv, length, yaw) in Walls())
            {
                for (float t = step / 2; t <= length - step / 2 + 0.01f; t += step)
                {
                    spots.Add((u + du * t, v + dv * t, yaw));
                }
            }
            return [.. spots.Where(s => !NearDoor(s.Item1, s.Item2, clear))];
        }
    }
}
