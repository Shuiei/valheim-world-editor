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
    /// Cellular noise (2D) at the point scaled by the frequency, as the cellular return type says.
    /// </summary>
    public FN_DECIMAL GetCellular(FN_DECIMAL x, FN_DECIMAL y)
    {
        x *= m_frequency;
        y *= m_frequency;

        return SingleCellular(x, y);
    }

    /// <summary>
    /// Cellular noise (2D): the nearest jittered cell point's random value or the distance to it (by the distance
    /// function). The point is already scaled by the frequency.
    /// </summary>
    private FN_DECIMAL SingleCellular(FN_DECIMAL x, FN_DECIMAL y)
    {
        int xr = FastRound(x);
        int yr = FastRound(y);

        FN_DECIMAL distance = 999999;
        int xc = 0, yc = 0;

        for (int xi = xr - 1; xi <= xr + 1; xi++)
        {
            for (int yi = yr - 1; yi <= yr + 1; yi++)
            {
                Float2 vec = CELL_2D[Hash2D(m_seed, xi, yi) & 255];

                FN_DECIMAL vecX = xi - x + vec.x * m_cellularJitter;
                FN_DECIMAL vecY = yi - y + vec.y * m_cellularJitter;

                FN_DECIMAL newDistance = CellDistance(m_cellularDistanceFunction, vecX, vecY);

                if (newDistance < distance)
                {
                    distance = newDistance;
                    xc = xi;
                    yc = yi;
                }
            }
        }

        switch (m_cellularReturnType)
        {
            case CellularReturnType.CellValue:
                return ValCoord2D(m_seed, xc, yc);

            case CellularReturnType.Distance:
                return distance;
            default:
                return 0;
        }
    }

    /// <summary>
    /// The distance of a cell point from the sample, by the distance function: squared Euclidean (the default for any
    /// other value), Manhattan, or Manhattan plus squared Euclidean ("natural").
    /// </summary>
    /// <param name="function">The distance function.</param>
    /// <param name="vecX">The point's x offset from the sample.</param>
    /// <param name="vecY">The point's y offset from the sample.</param>
    private static FN_DECIMAL CellDistance(CellularDistanceFunction function, FN_DECIMAL vecX, FN_DECIMAL vecY)
    {
        switch (function)
        {
            case CellularDistanceFunction.Manhattan:
                return Math.Abs(vecX) + Math.Abs(vecY);
            case CellularDistanceFunction.Natural:
                return (Math.Abs(vecX) + Math.Abs(vecY)) + (vecX * vecX + vecY * vecY);
            default:
                return vecX * vecX + vecY * vecY;
        }
    }
}
