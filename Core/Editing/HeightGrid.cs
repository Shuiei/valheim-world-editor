namespace TerrainEditor.Editing;

// The ground of a block of zones as one grid of heights (65 points per zone side, shared edges once):
// the generated ground plus the edits, within the game's ±8 m (plus the No limit lift not saved yet).
public static class HeightGrid
{
	public static (int W, int H, float[] Heights) Read(ValheimGen.TerrainService terrain, EditStore edits, int x0, int z0, int x1, int z1)
	{
		int w = (x1 - x0 + 1) * 64 + 1, h = (z1 - z0 + 1) * 64 + 1;
		float[] heights = new float[w * h];
		for (int zz = z0; zz <= z1; zz++)
		{
			for (int zx = x0; zx <= x1; zx++)
			{
				float[] b = terrain.BaseZone(zx, zz);
				ZoneEdit? e = edits.Get(zx, zz);
				int ox = (zx - x0) * 64, oz = (zz - z0) * 64;
				for (int k = 0; k < EditStore.Grid; k++)
				{
					for (int l = 0; l < EditStore.Grid; l++)
					{
						int i = k * EditStore.Grid + l;
						float v = b[i];
						if (e != null && e.Modified[i])
						{
							v = Math.Clamp(b[i] + e.Level[i] + e.Smooth[i], b[i] - EditStore.MaxLevel, b[i] + EditStore.MaxLevel);
						}
						v += e?.Lift[i] ?? 0;
						heights[(oz + k) * w + ox + l] = v;
					}
				}
			}
		}
		return (w, h, heights);
	}
}
