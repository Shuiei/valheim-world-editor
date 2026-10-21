using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Keeps what a mod holds of a place with the editor's copies (see <see cref="IExtensionRegistry.AddAreaCopier"/>):
/// what no object of the copy carries, such as rock dug out under a house that the mod records in objects only holding
/// its data. It goes with the copy (and the blueprint files made of it) and is put down where the copy is pasted.
/// </summary>
public interface IAreaCopier
{
    /// <summary>
    /// What the mod holds of a place being copied, in a form of its own; null when nothing. Keep places as offsets from
    /// the copy's middle (<see cref="CopyRegion.Middle"/>): the copy may be pasted anywhere, turned and mirrored, in
    /// another world too.
    /// </summary>
    /// <param name="area">The area copied from.</param>
    /// <param name="region">What is copied.</param>
    byte[]? Copy(IAreaView area, CopyRegion region);

    /// <summary>
    /// Puts down what <see cref="Copy"/> kept, once for a paste and all its copies (a repeated paste lands several):
    /// the edit is made part of the paste's undo step. Null when there is nothing to put down.
    /// </summary>
    /// <param name="area">The area pasted into, its ground as the paste leaves it (its new objects not in it yet).</param>
    /// <param name="kept">What <see cref="Copy"/> returned.</param>
    /// <param name="copies">Where each copy lands.</param>
    ToolEdit? Paste(IAreaView area, byte[] kept, IReadOnlyList<CopyPlacement> copies);
}
