#pragma warning disable
#define FN_USE_DOUBLES

#if FN_USE_DOUBLES
using FN_DECIMAL = System.Double;
#else
using FN_DECIMAL = System.Single;
#endif
using System.Runtime.CompilerServices;

namespace ValheimGen;

public partial class FastNoise
{
    /// <summary>
    /// The seed and a 2D grid point mixed into a hash.
    /// </summary>
    [MethodImplAttribute(FN_INLINE)]
    private static int Hash2D(int seed, int x, int y)
    {
        int hash = seed;
        hash ^= X_PRIME * x;
        hash ^= Y_PRIME * y;

        hash = hash * hash * hash * 60493;
        hash = (hash >> 13) ^ hash;

        return hash;
    }

    /// <summary>
    /// A random value, -1..1, for a 2D grid point.
    /// </summary>
    [MethodImplAttribute(FN_INLINE)]
    private static FN_DECIMAL ValCoord2D(int seed, int x, int y)
    {
        int n = seed;
        n ^= X_PRIME * x;
        n ^= Y_PRIME * y;

        return (n * n * n * 60493) / (FN_DECIMAL)2147483648.0;
    }

    /// <summary>
    /// The 2D grid point's hashed gradient dotted with the offset (xd, yd) from it.
    /// </summary>
    [MethodImplAttribute(FN_INLINE)]
    private static FN_DECIMAL GradCoord2D(int seed, int x, int y, FN_DECIMAL xd, FN_DECIMAL yd)
    {
        int hash = seed;
        hash ^= X_PRIME * x;
        hash ^= Y_PRIME * y;

        hash = hash * hash * hash * 60493;
        hash = (hash >> 13) ^ hash;

        Float2 g = GRAD_2D[hash & 7];

        return xd * g.x + yd * g.y;
    }
}
