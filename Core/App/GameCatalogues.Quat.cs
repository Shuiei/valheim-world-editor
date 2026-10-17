using AssetsTools.NET;

namespace TerrainEditor.App;

/// <summary>
/// A rotation of the game data: its product, its turn of a vector and its rounded parts.
/// </summary>
public static partial class GameCatalogues
{
    /// <summary>
    /// A rotation (x, y, z, w). A prefab root's frame is the identity in whole numbers (Whole): turned
    /// negative, its zeros stay 0.0 where computed ones turn -0.0, and the sign of a zero shows in the
    /// files ("-0.0"); written, its parts are whole numbers ("0", "1").
    /// </summary>
    /// <param name="X">The x part.</param>
    /// <param name="Y">The y part.</param>
    /// <param name="Z">The z part.</param>
    /// <param name="W">The w part.</param>
    /// <param name="Whole">The root's identity in whole numbers, written as such (see the summary).</param>
    private readonly record struct Quat(double X, double Y, double Z, double W, bool Whole = false)
    {
        /// <summary>
        /// The prefab root's frame: the identity, in whole numbers.
        /// </summary>
        public static readonly Quat Root = new(0, 0, 0, 1, true);

        /// <summary>
        /// A Quaternion field's rotation.
        /// </summary>
        public static Quat Of(AssetTypeValueField q) => new(q["x"].AsFloat, q["y"].AsFloat, q["z"].AsFloat, q["w"].AsFloat);

        /// <summary>
        /// Negated: for a whole rotation 0 - v, so a zero stays 0.0.
        /// </summary>
        private double Neg(double v) => Whole ? 0 - v : -v;

        /// <summary>
        /// The product this * b (b applied first).
        /// </summary>
        public Quat Mul(Quat b) => new(
            W * b.X + X * b.W + Y * b.Z - Z * b.Y,
            W * b.Y - X * b.Z + Y * b.W + Z * b.X,
            W * b.Z + X * b.Y - Y * b.X + Z * b.W,
            W * b.W - X * b.X - Y * b.Y - Z * b.Z);

        /// <summary>
        /// v turned by this rotation (q v q^-1).
        /// </summary>
        public (double X, double Y, double Z) Rotate(double vx, double vy, double vz)
        {
            double nx = Neg(X), ny = Neg(Y), nz = Neg(Z);
            double ix = W * vx + Y * vz - Z * vy, iy = W * vy + Z * vx - X * vz, iz = W * vz + X * vy - Y * vx, iw = nx * vx - Y * vy - Z * vz;
            return (ix * W + iw * nx + iy * nz - iz * ny, iy * W + iw * ny + iz * nx - ix * nz, iz * W + iw * nz + ix * ny - iy * nx);
        }

        /// <summary>
        /// v turned by this rotation.
        /// </summary>
        public (double X, double Y, double Z) Rotate((double X, double Y, double Z) v) => Rotate(v.X, v.Y, v.Z);

        /// <summary>
        /// Its parts rounded to n decimals (whole numbers stay whole).
        /// </summary>
        public object[] Rounded(int n) => Whole ? [0L, 0L, 0L, 1L] : [R(X, n), R(Y, n), R(Z, n), R(W, n)];
    }
}
