namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Places the parts of a mod's linked objects (a manifest's links) where the mod builds them when their data alone
/// does not say it (see <see cref="IExtensionRegistry.AddLinkPlacer"/>): the parts of a swivel the mod has turned, a
/// part the mod puts somewhere else than its offset.
/// </summary>
public interface ILinkPlacer
{
    /// <summary>
    /// The frame the parts hanging on an object hang in, in the object's own frame (a swivel's platform, turned or moved
    /// as the mod leaves it); null: the object's own frame. Called for the objects of the placer's prefabs that have
    /// parts.
    /// </summary>
    /// <param name="obj">The object, as drawn.</param>
    LinkFrame? FrameOf(IObjectView obj);

    /// <summary>
    /// Where a part stands in the frame of what it hangs on, in place of the offset and turn its data holds; null: as
    /// its data says. The frame <see cref="FrameOf"/> gives for the parent is not applied then. Called for the parts of
    /// the placer's prefabs.
    /// </summary>
    /// <param name="part">The part, at the place and turn the game saved it.</param>
    /// <param name="parent">What it hangs on, as drawn.</param>
    LinkFrame? PlaceOf(IObjectView part, IObjectView parent);
}
