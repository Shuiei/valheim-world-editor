using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>Gives the shapes the editor draws for an object besides its model (see <see cref="IExtensionRegistry.AddShapes"/>).</summary>
public interface IShapeProvider
{
    /// <summary>The object's shapes, in its own frame (none when there is nothing to draw).</summary>
    /// <param name="obj">The object, as drawn.</param>
    IEnumerable<Shape> ShapesOf(IObjectView obj);
}
