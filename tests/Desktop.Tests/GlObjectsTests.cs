using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// An owner of OpenGL objects never deletes a name it does not hold (it may be another's now): nothing reaches OpenGL
/// for those.
/// </summary>
public class GlObjectsTests
{
    [Fact]
    public void NamesNotHeldAreNotDeleted()
    {
        // No OpenGL at all: any call to it would fail.
        var own = new GlObjects(null!);
        own.DeleteBuffer(1);
        own.DeleteVertexArray(2);
        own.DeleteTexture(3);
        own.DeleteFramebuffer(4);
        own.DeleteRenderbuffer(5);
        own.DeleteAll();
    }
}
