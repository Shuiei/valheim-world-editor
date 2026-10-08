namespace TerrainEditor.Desktop;

// The Script tool's examples: ready to run on any open area, and to start a script of one's own from.
public static class ScriptExamples
{
	public sealed record Example(string Name, string Code);

	public static readonly Example[] All =
	{
		new("Mountain range across the area", """
			// A range of peaks across the area, from south-west to north-east, each one different.
			Rnd.Seed = 7;
			int peaks = 4;
			for (int i = 0; i < peaks; i++)
			{
			    float t = (i + 0.5f) / peaks;
			    float x = Area.MinX + (Area.MaxX - Area.MinX) * (0.2f + 0.6f * t);
			    float z = Area.MinZ + (Area.MaxZ - Area.MinZ) * (0.2f + 0.6f * t) + Rnd.Range(-20, 20);
			    Ground.Mountain(x, z, "Lone peak", height: Rnd.Range(35, 60), radius: Rnd.Range(60, 80), seed: Rnd.Int(1, 99999));
			}
			Print("Done: Save to keep it (it becomes ground discs).");
			"""),
		new("Terraced hill", """
			// A hill in the middle of the area with flat terraces, like rice fields, painted dirt at the edges.
			float cx = Area.CenterX, cz = Area.CenterZ, radius = 60, height = 30, steps = 6;
			// The trees, rocks and bushes it would bury go first (buildings stay).
			foreach (var o in Objects.Near(cx, cz, radius).Where(o => !o.Building))
			    Objects.Remove(o);
			Ground.Shape(cx, cz, radius, (dx, dz) =>
			{
			    float d = MathF.Sqrt(dx * dx + dz * dz) / radius;
			    float smooth = height * (1 - d * d);
			    float step = height / steps;
			    float terraced = MathF.Floor(smooth / step) * step;
			    // Flat terraces, each rising to the next over the last 15% of its width.
			    float frac = (smooth - terraced) / step;
			    return terraced + step * MathF.Max(0, (frac - 0.85f) / 0.15f);
			});
			foreach (var (x, z) in Area.Points())
			{
			    float d = MathF.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
			    if (d < radius && Ground.Height(x + 1, z) - Ground.Height(x, z) is > 0.5f or < -0.5f)
			        Ground.Paint(x, z, "dirt", 0.8f);
			}
			"""),
		new("Canyon with a river", """
			// A winding canyon across the area, 25 m deep, with water at its bottom.
			Ground.NoLimit = true;
			float width = 18;
			foreach (var (x, z) in Area.Points())
			{
			    // The canyon's line winds with noise; its depth fades at the area's edges.
			    float line = Area.CenterZ + 40 * Noise.At(x, 0, 120, seed: 3);
			    float d = MathF.Abs(z - line) / width;
			    if (d >= 1) continue;
			    float edge = MathF.Min(1, MathF.Min(x - Area.MinX, Area.MaxX - x) / 30);
			    float bed = Area.Water - 3;
			    float target = bed + (Ground.Height(x, z) - bed) * d * d;
			    Ground.Set(x, z, Ground.Height(x, z) + (target - Ground.Height(x, z)) * edge);
			}
			"""),
		new("Scatter boulders", """
			// Big boulders scattered where the noise is high, more on slopes, sized at random.
			Rnd.Seed = 11;
			int placed = 0;
			foreach (var (x, z) in Area.Points(step: 6))
			{
			    float slope = MathF.Abs(Ground.Height(x + 2, z) - Ground.Height(x - 2, z)) / 4;
			    if (Noise.At(x, z, 60, seed: 5) > 0.25f && Rnd.Chance(0.15f + slope))
			    {
			        Objects.Place(Rnd.Pick("rock4_forest", "rock4_coast", "rock4_heath"), x + Rnd.Range(-2, 2), z + Rnd.Range(-2, 2),
			            yaw: Rnd.Range(0, 360), scale: Rnd.Range(0.25f, 0.6f));
			        placed++;
			    }
			}
			Print($"{placed} boulders placed.");
			"""),
		new("Ring of trees", """
			// A ring of beeches around the middle of the area (the trees standing inside it are taken away).
			float cx = Area.CenterX, cz = Area.CenterZ, radius = 40;
			foreach (var o in Objects.OfKind("Trees").ToList())
			    if ((o.X - cx) * (o.X - cx) + (o.Z - cz) * (o.Z - cz) < radius * radius) Objects.Remove(o);
			Rnd.Seed = 3;
			for (int i = 0; i < 36; i++)
			{
			    float a = i * MathF.Tau / 36;
			    Objects.Place("Beech1", cx + MathF.Cos(a) * radius, cz + MathF.Sin(a) * radius, yaw: Rnd.Range(0, 360), scale: Rnd.Range(0.9f, 1.2f));
			}
			"""),
		new("Flat paved base", """
			// A flat, paved square for a base in the middle of the area, at its average height, with a
			// gentle slope back to the ground around it.
			float cx = Area.CenterX, cz = Area.CenterZ, half = 20, blend = 10;
			Ground.NoLimit = false;
			float sum = 0; int n = 0;
			for (float z = cz - half; z <= cz + half; z++)
			    for (float x = cx - half; x <= cx + half; x++) { sum += Ground.Height(x, z); n++; }
			float level = sum / n;
			for (float z = cz - half - blend; z <= cz + half + blend; z++)
			    for (float x = cx - half - blend; x <= cx + half + blend; x++)
			    {
			        float out_ = MathF.Max(MathF.Max(MathF.Abs(x - cx) - half, MathF.Abs(z - cz) - half), 0) / blend;
			        if (out_ >= 1) continue;
			        float k = 1 - out_ * out_ * (3 - 2 * out_);
			        Ground.Set(x, z, Ground.Height(x, z) + (level - Ground.Height(x, z)) * k);
			        if (out_ == 0) Ground.Paint(x, z, "paved");
			    }
			Print($"Levelled to {level:0.0} m.");
			"""),
		new("Noise landscape", """
			// Rolling land over the whole area: broad hills and valleys from noise, up to 25 m either way.
			Ground.NoLimit = true;
			foreach (var (x, z) in Area.Points())
			{
			    float h = 25 * Noise.At(x, z, 140, seed: 1) + 6 * Noise.At(x, z, 40, seed: 2);
			    // Back to the ground as it was near the area's edges.
			    float edge = MathF.Min(1, MathF.Min(MathF.Min(x - Area.MinX, Area.MaxX - x), MathF.Min(z - Area.MinZ, Area.MaxZ - z)) / 40);
			    Ground.Raise(x, z, h * edge);
			}
			"""),
	};
}
