namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The world around an object, for shapes that span objects (a rope between two anchors, a cable between posts):
/// finds another object by an id it holds.
/// </summary>
public interface IWorldView
{
    /// <summary>
    /// The object holding an id, as drawn; false when none in the area does. Only ids under a key a <c>links</c>
    /// rule of a manifest names as its <c>id</c> are found (the editor indexes those).
    /// </summary>
    /// <param name="idKey">The data key of the id, as the mod's code names it.</param>
    /// <param name="id">The id.</param>
    /// <param name="obj">The object found.</param>
    bool TryFind(string idKey, int id, out IObjectView obj);
}
