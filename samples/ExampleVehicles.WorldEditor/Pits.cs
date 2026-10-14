using System;
using System.Collections.Generic;
using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ExampleVehicles.WorldEditor;

/// <summary>
/// The pits (Keys.Pit): the mod digs a round hole in the ground under each while the game runs, as wide as its
/// radius says. The ground is not drawn inside, so the pit's own walls (its model) show.
/// </summary>
public sealed class Pits : IGroundCutter
{
    /// <summary>A pit's radius when it keeps none, in metres.</summary>
    private const float DefaultRadius = 1.5f;

    /// <summary>How many points round the outline.</summary>
    private const int Sides = 16;

    /// <inheritdoc/>
    public IEnumerable<GroundCut> CutsOf(IObjectView obj, IWorldView world)
    {
        float r = obj.TryGetFloat(Keys.Radius, out float given) && given > 0 ? given : DefaultRadius;
        var outline = new Vector2[Sides];
        for (int i = 0; i < Sides; i++)
        {
            double a = 2 * Math.PI * i / Sides;
            outline[i] = new Vector2((float)(r * Math.Cos(a)), (float)(r * Math.Sin(a)));
        }
        yield return new GroundCut(outline);
    }
}
