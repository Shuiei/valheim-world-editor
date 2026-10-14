namespace ValheimWorldEditor.Sdk;

/// <summary>
/// The ids a pasted copy's objects got: each id a linked object held (a manifest's links "id"), mapped to the new one
/// its copy holds. Ids of objects that were not copied have none.
/// </summary>
public interface IIdMap
{
    /// <summary>The copy's id for an original's; false when the object holding it was not copied.</summary>
    bool TryGetNew(int oldId, out int newId);
}
