using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>Gives where the ground is not drawn for an object (see <see cref="IExtensionRegistry.AddGroundCuts"/>).</summary>
public interface IGroundCutter
{
    /// <summary>The object's cuts (none when the ground stays whole).</summary>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="world">The world around it, to find the objects a cut reaches.</param>
    IEnumerable<GroundCut> CutsOf(IObjectView obj, IWorldView world);
}
