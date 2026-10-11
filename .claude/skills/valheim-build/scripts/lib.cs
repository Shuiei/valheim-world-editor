// Helper library for the editor's run_script (Valheim World Editor). run_script scripts are
// self-contained: paste the part you need above the build. Everything below was proven in a castle build
// the user approved; the comments say why each detail is the way it is.
//
// Conventions: G = ground height (34 on the Workshop plot). Facing φ: 0 north, 90 east, 180 south,
// 270 west. n = (sin φ, cos φ) outward, t = (cos φ, −sin φ) along the wall. Walls are 2 m x 1 m stone
// pieces on a 2 m grid with their middle line on the given edges; rows of 1 m.

const float G = 34;
int n = 0;
void P(string p, float x, float z, float yaw, float y) { Objects.Place(p, x, z, yaw, 0, y); n++; }
float S(float d) => MathF.Sin(d * MathF.PI / 180);
float C(float d) => MathF.Cos(d * MathF.PI / 180);
int Mod4(float c) => (((int)MathF.Round(c)) % 4 + 4) % 4;

// Openings (gates, passages): no wall piece whose middle falls inside.
var holes = new List<(float x0, float x1, float z0, float z1, float y0, float y1)>();
bool InHole(float x, float z, float y) => holes.Any(h => x > h.x0 && x < h.x1 && z > h.z0 && z < h.z1 && y > h.y0 && y < h.y1);

// ---- Windows. kind: "leaded" (3 high, crystal behind an iron lattice: the approved look), "tall"
// (4 high, leaded), "bar" (3 high, an iron grate with glass behind: ground floors), "carved" (3 high,
// darkwood screens: one showpiece). 2 m wide, arched head of two quarter arches on the top row, a
// darkwood sill. Every part takes yaw φ: never 0/90 (round towers).
int WindowHeight(string kind) => kind == "tall" ? 4 : 3;
void Window(float cx, float cz, float phi, float y, string kind)
{
	float nx = S(phi), nz = C(phi), tx = C(phi), tz = -S(phi);
	int h = WindowHeight(kind);
	P("Piece_grausten_wall_arch", cx - 0.5f * tx, cz - 0.5f * tz, phi + 180, y + h - 1);
	P("Piece_grausten_wall_arch", cx + 0.5f * tx, cz + 0.5f * tz, phi, y + h - 1);
	if (kind == "bar")
	{
		P("iron_grate", cx - nx * 0.25f, cz - nz * 0.25f, phi, y + 1);
	}
	for (int r = 0; r < h; r++)
		foreach (float s in new[] { -0.5f, 0.5f })
		{
			float back = kind == "bar" ? 0.4f : 0.2f;
			if (kind == "carved" && r < h - 1)
			{
				if (r % 2 == 0) P("darkwood_decowall", cx - nx * 0.1f + tx * s, cz - nz * 0.1f + tz * s, phi, y + r);
				continue;
			}
			P("crystal_wall_1x1", cx - nx * back + tx * s, cz - nz * back + tz * s, phi, y + r);
			if (kind is "leaded" or "tall") P("iron_wall_1x1", cx - nx * 0.02f + tx * s, cz - nz * 0.02f + tz * s, phi, y + r);
		}
	P("darkwood_beam", cx + nx * 0.6f, cz + nz * 0.6f, phi, y - 0.2f);
}

// ---- A straight wall from a to b (along x or z) facing φ, `rows` high, with windows on the shared 4 m
// grid (column centre ≡ 1 mod 4, never the end pieces) unless cols says otherwise; a 2-row plinth, a
// ledge at the first storey line, and a cornice (a course + dentils, dentils only between windows so no
// stone lands on a window's arch). st: (bottom row, kind) per storey; a window's top must stay at least
// 2 rows under the wall top (the cornice zone).
void Facade(float ax, float az, float bx, float bz, float phi, int rows, (int b, string k)[] st, Func<float, bool>? cols = null, bool cornice = true, int ledgeRow = 5)
{
	float len = MathF.Abs(bx - ax) + MathF.Abs(bz - az);
	int cnt = (int)MathF.Round(len / 2);
	float dx = (bx - ax) / len, dz = (bz - az) / len, nx = S(phi), nz = C(phi);
	float yaw = MathF.Abs(dx) > 0.5f ? 0 : 90;
	var stOk = st.Where(s => s.b + WindowHeight(s.k) <= rows - 2).ToArray();
	for (int i = 0; i < cnt; i++)
	{
		float x = ax + dx * (2 * i + 1), z = az + dz * (2 * i + 1), c = yaw == 0 ? x : z;
		bool wc = i > 0 && i < cnt - 1 && (cols != null ? cols(c) : Mod4(c) == 1);
		for (int r = 0; r < rows; r++)
		{
			float y = G + 0.5f + r;
			if (InHole(x, z, y)) continue;
			if (wc && stOk.Any(s => r >= s.b && r < s.b + WindowHeight(s.k) && !InHole(x, z, G + s.b + 0.5f))) continue;
			P("stone_wall_2x1", x, z, yaw, y);
		}
		if (wc) foreach (var s in stOk) if (!InHole(x, z, G + s.b + 0.5f)) Window(x, z, phi, G + s.b, s.k);
		for (int r = 0; r < 2; r++) if (!InHole(x, z, G + 0.5f + r)) P("stone_wall_2x1", x + nx * 0.35f, z + nz * 0.35f, yaw, G + 0.5f + r);
		if (ledgeRow > 0) P("stone_wall_2x1", x + nx * 0.3f, z + nz * 0.3f, yaw, G + ledgeRow + 0.5f);
		if (cornice)
		{
			P("stone_wall_2x1", x + nx * 0.45f, z + nz * 0.45f, yaw, G + rows - 0.5f);
			if (!wc) P("stone_wall_1x1", x - dx * 0.5f + nx * 0.25f, z - dz * 0.5f + nz * 0.25f, yaw, G + rows - 1.5f);
		}
	}
}

// ---- Battlements on a wall's top: two steps of corbels, a parapet standing 0.9 m out, merlons.
void Crown(float ax, float az, float bx, float bz, float phi, float top)
{
	float len = MathF.Abs(bx - ax) + MathF.Abs(bz - az);
	float dx = (bx - ax) / len, dz = (bz - az) / len, nx = S(phi), nz = C(phi);
	float yaw = MathF.Abs(dx) > 0.5f ? 0 : 90;
	for (float s = 0.5f; s < len; s += 2)
	{
		P("stone_wall_1x1", ax + dx * s + nx * 0.3f, az + dz * s + nz * 0.3f, yaw, top - 1.5f);
		P("stone_wall_1x1", ax + dx * s + nx * 0.6f, az + dz * s + nz * 0.6f, yaw, top - 0.5f);
	}
	for (float s = -1; s <= len + 1; s += 2) P("stone_wall_2x1", ax + dx * s + nx * 0.9f, az + dz * s + nz * 0.9f, yaw, top + 0.5f);
	for (float s = -0.5f; s <= len + 0.5f; s += 2) P("stone_wall_1x1", ax + dx * s + nx * 0.9f, az + dz * s + nz * 0.9f, yaw, top + 1.5f);
}

void Flat(float x0, float z0, float x1, float z1, float top)
{
	for (float x = x0 + 1; x < x1; x += 2) for (float z = z0 + 1; z < z1; z += 2) P("stone_floor_2x2", x, z, 0, top - 0.5f);
}

// ---- A 2x2 stone spire (four quarter spires) with its base at y.
void Spire(float cx, float cz, float y)
{
	P("blackmarble_tip", cx + 0.5f, cz - 0.5f, 0, y); P("blackmarble_tip", cx - 0.5f, cz - 0.5f, 90, y);
	P("blackmarble_tip", cx - 0.5f, cz + 0.5f, 180, y); P("blackmarble_tip", cx + 0.5f, cz + 0.5f, 270, y);
}

// ---- Red tile roof (grausten, 45°) over a 12 m deep wing, ridge along x or z, eave = wall top.
void TileRoof(float x0, float z0, float x1, float z1, float eave, bool alongX)
{
	if (alongX)
		for (float x = x0 + 1; x < x1; x += 2)
			for (int o = 1; o <= 5; o += 2) { P("piece_grausten_roof_45", x, z0 + o, 180, eave + o); P("piece_grausten_roof_45", x, z1 - o, 0, eave + o); }
	else
		for (float z = z0 + 1; z < z1; z += 2)
			for (int o = 1; o <= 5; o += 2) { P("piece_grausten_roof_45", x0 + o, z, 270, eave + o); P("piece_grausten_roof_45", x1 - o, z, 90, eave + o); }
}

// ---- Crow-stepped gable closing a 12 m roof end (a wall along x from (gx, gz), or along z), spire on top.
// Put one at every roof end that is not buried in a taller neighbour: open roof ends look like stray triangles.
void CrowGable(float gx, float gz, bool alongX, float eave)
{
	for (int p = 0; p < 3; p++)
		for (int r = 0; r < 2; r++)
			for (float s = 2 * p + 1; s < 12 - 2 * p; s += 2)
				if (alongX) P("stone_wall_2x1", gx + s, gz, 0, eave + 0.5f + 2 * p + r);
				else P("stone_wall_2x1", gx, gz + s, 90, eave + 0.5f + 2 * p + r);
	if (alongX) Spire(gx + 6, gz, eave + 6); else Spire(gx, gz + 6, eave + 6);
}

// ---- Chimney on a ridge (base inside the roof).
void Chimney(float cx, float cz, float baseY, int h)
{
	for (int r = 0; r < h; r++) { P("stone_wall_2x1", cx, cz - 0.5f, 0, baseY + 0.5f + r); P("stone_wall_2x1", cx, cz + 0.5f, 0, baseY + 0.5f + r); }
	P("stone_wall_2x1", cx, cz - 0.7f, 0, baseY + h + 0.5f); P("stone_wall_2x1", cx, cz + 0.7f, 0, baseY + h + 0.5f);
}

// ---- Cone spire of 67° shingles: rings 2 m in and 4 m up. Height ≈ 4 × rings; rOut ≥ 0.9.
// Tip y = (highest ring's origin) + 3: put a flagpole (darkwood_pole4, centred) at tip + 2.
void Cone(float cx, float cz, float rOut, float baseY)
{
	for (int i = 0; ; i++)
	{
		float ro = rOut - 2 * i;
		if (ro < 0.9f) break;
		int cnt = Math.Max(4, (int)MathF.Ceiling(2 * MathF.PI * ro / 1.9f));
		for (int k = 0; k < cnt; k++) { float a = 360f * k / cnt; P("darkwood_roof_67", cx + (ro - 1) * S(a), cz + (ro - 1) * C(a), a, baseY + 4 * i + 1); }
	}
}

// ---- Round tower: 2 m pieces turned round a circle (spacing 1.7 m so they overlap), windows facing the
// given directions (degrees), plinth ring, ledge rings every 5 m, battlements and (cone) an inner drum and a
// cone behind them, or (crown false) a cone straight on the wall top. Windows: keep their tops under the
// crown's corbels (top − 2).
void Round(float cx, float cz, float R, float y0, int rows, (int b, string k)[] st, float[] winDirs, bool cone = true, bool crown = true)
{
	int cnt = (int)MathF.Ceiling(2 * MathF.PI * R / 1.7f);
	var winK = winDirs.Select(d => (int)MathF.Round(d / 360 * cnt) % cnt).ToHashSet();
	var stOk = st.Where(s => s.b + WindowHeight(s.k) <= rows - 2).ToArray();
	for (int k = 0; k < cnt; k++)
	{
		float a = 360f * k / cnt, x = cx + R * S(a), z = cz + R * C(a);
		bool wc = winK.Contains(k);
		for (int r = 0; r < rows; r++)
		{
			if (wc && stOk.Any(s => r >= s.b && r < s.b + WindowHeight(s.k))) continue;
			P("stone_wall_2x1", x, z, a, y0 + 0.5f + r);
		}
		if (wc) foreach (var s in stOk) Window(x, z, a, y0 + s.b, s.k);
		if (y0 <= G) for (int r = 0; r < 2; r++) P("stone_wall_2x1", cx + (R + 0.35f) * S(a), cz + (R + 0.35f) * C(a), a, G + 0.5f + r);
		for (int b = 5; b < rows - 3; b += 5) P("stone_wall_2x1", cx + (R + 0.3f) * S(a), cz + (R + 0.3f) * C(a), a, y0 + b + 0.5f);
	}
	float top = y0 + rows;
	for (float x = cx - R + 1; x <= cx + R; x += 2)
		for (float z = cz - R + 1; z <= cz + R; z += 2)
			if ((x - cx) * (x - cx) + (z - cz) * (z - cz) <= (R - 0.3f) * (R - 0.3f)) P("stone_floor_2x2", x, z, 0, top - 0.5f);
	if (crown)
	{
		int c1 = (int)MathF.Ceiling(2 * MathF.PI * (R + 0.6f) / 2);
		for (int k = 0; k < c1; k++)
		{
			float a = 360f * (k + 0.5f) / c1;
			P("stone_wall_1x1", cx + (R + 0.3f) * S(a), cz + (R + 0.3f) * C(a), a, top - 1.5f);
			P("stone_wall_1x1", cx + (R + 0.6f) * S(a), cz + (R + 0.6f) * C(a), a, top - 0.5f);
		}
		float Rp = R + 0.9f;
		int c2 = (int)MathF.Ceiling(2 * MathF.PI * Rp / 1.8f), c3 = (int)MathF.Ceiling(2 * MathF.PI * Rp / 2);
		for (int k = 0; k < c2; k++) { float a = 360f * k / c2; P("stone_wall_2x1", cx + Rp * S(a), cz + Rp * C(a), a, top + 0.5f); }
		for (int k = 0; k < c3; k++) { float a = 360f * (k + 0.5f) / c3; P("stone_wall_1x1", cx + Rp * S(a), cz + Rp * C(a), a, top + 1.5f); }
	}
	if (!cone) return;
	if (crown)
	{
		float Ri = R - 1.2f;
		int ci = (int)MathF.Ceiling(2 * MathF.PI * Ri / 1.7f);
		for (int k = 0; k < ci; k++) { float a = 360f * k / ci; for (int r = 0; r < 3; r++) P("stone_wall_2x1", cx + Ri * S(a), cz + Ri * C(a), a, top + 0.5f + r); }
		Cone(cx, cz, Ri + 1, top + 3);
	}
	else Cone(cx, cz, R + 0.8f, top);
}

// ---- Bartizan: a small turret corbelled out from a corner (centre 0.8 m out diagonally), with a cone.
// Keep it clear of neighbouring turrets' cones.
void Bartizan(float cx, float cz, float baseY, int rows)
{
	P("stone_wall_1x1", cx, cz, 45, baseY - 1.5f);
	for (int k = 0; k < 4; k++) { float a = 45 + 90 * k; P("stone_wall_1x1", cx + 0.6f * S(a), cz + 0.6f * C(a), a, baseY - 0.5f); }
	for (int k = 0; k < 6; k++) { float a = 60f * k; for (int r = 0; r < rows; r++) P("stone_wall_2x1", cx + 1.4f * S(a), cz + 1.4f * C(a), a, baseY + 0.5f + r); }
	Cone(cx, cz, 2.2f, baseY + rows);
}

// ---- Porch over a door in a wall facing φ: a 4 m shingle canopy sloping away on two posts and a beam.
void Porch(float cx, float cz, float phi)
{
	float nx = S(phi), nz = C(phi), tx = C(phi), tz = -S(phi);
	foreach (float s in new[] { -1f, 1f }) P("darkwood_roof", cx + nx * 1.56f + tx * s, cz + nz * 1.56f + tz * s, phi, G + 3.0f);
	foreach (float s in new[] { -1.8f, 1.8f })
	{
		float px = cx + nx * 2.3f + tx * s, pz = cz + nz * 2.3f + tz * s;
		P("darkwood_pole", px, pz, 0, G + 1); P("wood_pole", px, pz, 0, G + 2.5f);
	}
	P("darkwood_beam4x4", cx + nx * 2.3f, cz + nz * 2.3f, MathF.Abs(nz) > 0.5f ? 0 : 90, G + 3.05f);
}

// ---- Timber framing for a hall along x (walls at z0 and z1, floor at fy, ceiling at cy): posts every
// 4 m at x ≡ 3 mod 4 (between the windows), beams across, knee braces. The biggest interior improvement.
void HallFraming(float x0, float x1, float z0, float z1, float fy, float cy)
{
	for (float x = x0 + 3; x < x1; x += 4)
	{
		foreach (float pz in new[] { z0 + 0.8f, z1 - 0.8f })
			for (float y = fy; y < cy - 0.1f; y += 2) P("darkwood_pole", x, pz, 0, y + 1);
		for (float bz = z0 + 2; bz < z1; bz += 4) P("darkwood_beam4x4", x, bz, 90, cy - 0.25f);
		P("darkwood_beam_45", x, z0 + 2, 0, cy - 1.4f);
		P("darkwood_beam_45", x, z1 - 2, 180, cy - 1.4f);
	}
}

// ---- A courtyard window turned into a door: the lower 2 m become a wood door, the glass above stays as a
// fanlight. Remove the row-0 wall piece, the two plinth pieces, the bottom panes and the sill first
// (see references/audit.md for finding window parts), then: P("wood_door", cx, cz, phi, G + 1);
