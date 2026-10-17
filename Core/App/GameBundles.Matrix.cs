using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// The matrices of the model walk: a placement's product, and a transform from its position, rotation and scale.
/// </summary>
public sealed partial class GameBundles
{
    /// <summary>
    /// Matrices: row-major 4 x 4, column vectors (Unity's).
    /// </summary>
    public static class Matrix
    {
        /// <summary>
        /// The identity matrix.
        /// </summary>
        public static float[] Identity() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

        /// <summary>
        /// The product a * b (b applied first).
        /// </summary>
        public static float[] Mul(float[] a, float[] b)
        {
            float[] r = new float[16];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    float s = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        s += a[i * 4 + k] * b[k * 4 + j];
                    }
                    r[i * 4 + j] = s;
                }
            }
            return r;
        }

        /// <summary>
        /// A Unity transform as a matrix: translate * rotate * scale.
        /// </summary>
        /// <param name="px">The position's x.</param>
        /// <param name="py">The position's y.</param>
        /// <param name="pz">The position's z.</param>
        /// <param name="x">The rotation quaternion's x.</param>
        /// <param name="y">The rotation quaternion's y.</param>
        /// <param name="z">The rotation quaternion's z.</param>
        /// <param name="w">The rotation quaternion's w.</param>
        /// <param name="sx">The scale along x.</param>
        /// <param name="sy">The scale along y.</param>
        /// <param name="sz">The scale along z.</param>
        public static float[] Trs(float px, float py, float pz, float x, float y, float z, float w, float sx, float sy, float sz) =>
        [
            (1 - 2 * (y * y + z * z)) * sx, 2 * (x * y - z * w) * sy, 2 * (x * z + y * w) * sz, px,
            2 * (x * y + z * w) * sx, (1 - 2 * (x * x + z * z)) * sy, 2 * (y * z - x * w) * sz, py,
            2 * (x * z - y * w) * sx, 2 * (y * z + x * w) * sy, (1 - 2 * (x * x + y * y)) * sz, pz,
            0, 0, 0, 1,
        ];

        /// <summary>
        /// A Transform's local position, rotation and scale fields as a matrix.
        /// </summary>
        internal static float[] Trs(AssetTypeValueField p, AssetTypeValueField q, AssetTypeValueField s) =>
            Trs(p["x"].AsFloat, p["y"].AsFloat, p["z"].AsFloat, q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat, s["x"].AsFloat, s["y"].AsFloat, s["z"].AsFloat);
    }
}
