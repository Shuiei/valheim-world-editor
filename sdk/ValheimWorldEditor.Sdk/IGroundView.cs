namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The ground round an object, for shapes that reach it (a rope ladder hanging down to it): the world a shape or cut
/// hook is handed is one when the editor has the ground there (the 3D editor's area), so test for it
/// (<c>world is IGroundView ground</c>).
/// </summary>
public interface IGroundView
{
    /// <summary>The ground's height at a world point as the editor draws it, in metres; false outside the area it has.</summary>
    /// <param name="x">World x (east), in metres.</param>
    /// <param name="z">World z (north), in metres.</param>
    /// <param name="height">The height.</param>
    bool TryGroundHeight(float x, float z, out float height);
}
