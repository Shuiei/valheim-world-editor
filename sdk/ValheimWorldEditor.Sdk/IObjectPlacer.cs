using System.Collections.Generic;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Sets what a mod works out from where its objects stand, whenever the editor puts them at a place of its own (see
/// <see cref="IExtensionRegistry.AddPlacer"/>): a turn the mod keeps apart from the object's, the track a vehicle
/// stands on, the stop a sign names.
/// </summary>
public interface IObjectPlacer
{
    /// <summary>
    /// The objects of the placer's prefabs that one step of the editor puts at a place (placed, pasted, moved or turned,
    /// by any tool, a script or Claude), each in its new place with its values as they will be made (a pasted copy's
    /// after the copy rewriters): change their values in place. Objects changed where they stand (the inspector, a
    /// tool's edit) are no placement and do not come here.
    /// </summary>
    /// <param name="placed">The step's objects of the placer's prefabs, every one of them, so that what ties them to each
    /// other (a sign beside a track pasted with it) can be worked out.</param>
    /// <param name="area">The open area as it stands before the step, without the objects the step moves (their old
    /// places).</param>
    void Place(IReadOnlyList<ICopy> placed, IAreaView area);
}
