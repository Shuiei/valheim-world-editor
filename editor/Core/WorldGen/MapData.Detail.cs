using System.Collections.Concurrent;
using TerrainEditor.Editing;

namespace ValheimGen;

public sealed partial class MapData
{
    /// <summary>
    /// A 1 m detail window whose texel (0, 0) is the world vertex (x0, z0). Includes terrain edits.
    /// </summary>
    /// <param name="x0">World x of the first column, metres.</param>
    /// <param name="z0">World z of the first row, metres.</param>
    /// <param name="size">Texels per side.</param>
    /// <param name="editVersion">The edits' version: a new one builds the window again instead of using the cached
    /// one.</param>
    public Layers Detail(int x0, int z0, int size, int editVersion)
    {
        var key = (x0, z0, size, editVersion);
        lock (_detailsLock)
        {
            int i = _details.FindIndex(d => d.Key == key);
            if (i >= 0)
            {
                var hit = _details[i];
                _details.RemoveAt(i);
                _details.Add(hit);
                return hit.Layers;
            }
        }
        BeforeDetailBuild();
        Layers layers = BuildDetail(x0, z0, size);
        lock (_detailsLock)
        {
            // A build for older edits that ends after one for newer edits is given but not kept: the newer windows
            // stay.
            if (_details.Exists(d => d.Key.EditVersion > editVersion))
            {
                return layers;
            }
            // The edits' version only goes up: a window of another one is never asked for again.
            _details.RemoveAll(d => d.Key.EditVersion != editVersion || d.Key == key);
            _details.Add((key, layers));
            if (_details.Count > KeptDetails)
            {
                _details.RemoveAt(0);
            }
        }
        return layers;
    }

    /// <summary>
    /// Builds a detail window: each zone's base heights with its height and paint edits laid over them.
    /// </summary>
    internal Layers BuildDetail(int x0, int z0, int size)
    {
        Layers layers = new(size, size);
        // Gather the zones the window touches: base terrain and current edits.
        int zx0 = (int)Math.Floor((x0 + 32) / 64.0), zx1 = (int)Math.Floor((x0 + size - 1 + 32) / 64.0);
        int zz0 = (int)Math.Floor((z0 + 32) / 64.0), zz1 = (int)Math.Floor((z0 + size - 1 + 32) / 64.0);
        var zones = (from zz in Enumerable.Range(zz0, zz1 - zz0 + 1) from zx in Enumerable.Range(zx0, zx1 - zx0 + 1) select (zx, zz)).ToList();
        var bases = new ConcurrentDictionary<(int, int), float[]>();
        var edits = new ConcurrentDictionary<(int, int), ZoneEdit?>();
        Parallel.ForEach(zones, k =>
        {
            bases[k] = _terrain.BaseZone(k.zx, k.zz);
            edits[k] = _edits.Get(k.zx, k.zz);
        });
        Parallel.For(0, size, row =>
        {
            int wz = z0 + row;
            int zz = (int)Math.Floor((wz + 32) / 64.0);
            int k = wz - (zz * 64 - 32);
            for (int col = 0; col < size; col++)
            {
                int wx = x0 + col;
                int zx = (int)Math.Floor((wx + 32) / 64.0);
                int l = wx - (zx * 64 - 32);
                int vi = k * EditStore.Grid + l;
                float h = bases[(zx, zz)][vi];
                ZoneEdit? e = edits[(zx, zz)];
                (byte, byte, byte, byte)? paint = null;
                if (e != null)
                {
                    if (e.Modified[vi])
                    {
                        // TerrainComp.ApplyToHeightmap: never more than 8 m from the ground below.
                        h = Math.Clamp(h + e.Level[vi] + e.Smooth[vi], h - 8f, h + 8f);
                    }
                    if (e.PaintModified[vi])
                    {
                        paint = (Byte(e.Paint[vi * 4]), Byte(e.Paint[vi * 4 + 1]), Byte(e.Paint[vi * 4 + 2]), Byte(e.Paint[vi * 4 + 3]));
                    }
                }
                Heightmap.Biome biome = Generator.GetBiome(wx, wz);
                layers.Set(row * size + col, MapHeight(h, biome), Pixel(biome, wx, wz), MaskColor(wx, wz, h, biome), paint);
            }
        });
        return layers;
    }
}
