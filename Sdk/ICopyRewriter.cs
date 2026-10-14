namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Rewrites the objects of a pasted copy (see <see cref="IExtensionRegistry.AddCopyRewriter"/>): typically ids the
/// mod keeps in its own form (packed in a byte array), made to name the copy's objects instead of the originals'.
/// </summary>
public interface ICopyRewriter
{
    /// <summary>Rewrites one object of the copy, called once for each holding a key the rewriter handles.</summary>
    /// <param name="copy">The object, its values as they will be made; changed in place.</param>
    /// <param name="ids">The copy's new ids, for the originals' ones.</param>
    void Rewrite(ICopy copy, IIdMap ids);
}
