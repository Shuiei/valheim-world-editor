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
    /// One octave of simplex noise (2D) at a point already scaled by the frequency: gradients at the simplex's corners,
    /// summed with falloff.
    /// </summary>
    internal FN_DECIMAL SingleSimplex(int seed, FN_DECIMAL x, FN_DECIMAL y)
    {
        FN_DECIMAL t = (x + y) * F2;
        int i = FastFloor(x + t);
        int j = FastFloor(y + t);

        t = (i + j) * G2;
        FN_DECIMAL X0 = i - t;
        FN_DECIMAL Y0 = j - t;

        FN_DECIMAL x0 = x - X0;
        FN_DECIMAL y0 = y - Y0;

        int i1, j1;
        if (x0 > y0)
        {
            i1 = 1; j1 = 0;
        }
        else
        {
            i1 = 0; j1 = 1;
        }

        FN_DECIMAL x1 = x0 - i1 + G2;
        FN_DECIMAL y1 = y0 - j1 + G2;
        FN_DECIMAL x2 = x0 - 1 + 2 * G2;
        FN_DECIMAL y2 = y0 - 1 + 2 * G2;

        FN_DECIMAL n0, n1, n2;

        t = (FN_DECIMAL)0.5 - x0 * x0 - y0 * y0;
        if (t < 0) n0 = 0;
        else
        {
            t *= t;
            n0 = t * t * GradCoord2D(seed, i, j, x0, y0);
        }

        t = (FN_DECIMAL)0.5 - x1 * x1 - y1 * y1;
        if (t < 0) n1 = 0;
        else
        {
            t *= t;
            n1 = t * t * GradCoord2D(seed, i + i1, j + j1, x1, y1);
        }

        t = (FN_DECIMAL)0.5 - x2 * x2 - y2 * y2;
        if (t < 0) n2 = 0;
        else
        {
            t *= t;
            n2 = t * t * GradCoord2D(seed, i + 1, j + 1, x2, y2);
        }

        return 50 * (n0 + n1 + n2);
    }
}
