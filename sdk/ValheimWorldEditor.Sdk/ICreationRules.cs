using System.Collections.Generic;
using System.Numerics;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// What a mod does to objects as they are made (see <see cref="IExtensionRegistry.AddCreationRules"/>): a mod that
/// swaps, removes or adds objects, or sets their values, when the game creates them. Objects a step of the editor adds
/// (placed, pasted, regrown, by any tool, a script or Claude) are saved as they are and never created by the game, so
/// the editor hands them over as it makes them. Objects moved or changed where they stand are not made.
/// </summary>
public interface ICreationRules
{
    /// <summary>
    /// Whether objects of a prefab may be changed (asked once a prefab per batch, before any is handed over: keep it
    /// quick). Only those of the prefabs answered true come to <see cref="Created"/>.
    /// </summary>
    /// <param name="prefab">The prefab's name.</param>
    bool Covers(string prefab);

    /// <summary>
    /// A batch of objects just made, those of the prefabs <see cref="Covers"/> took: change their values in place,
    /// remove them, or add objects beside them. The objects added are made as given; they are not handed back here.
    /// </summary>
    /// <param name="made">The objects, and what can be done to them.</param>
    void Created(ICreation made);
}

/// <summary>A batch of objects just made, handed to <see cref="ICreationRules.Created"/>.</summary>
public interface ICreation
{
    /// <summary>The objects, their values as they will be made (set them here); in the order made.</summary>
    IReadOnlyList<ICopy> Objects { get; }

    /// <summary>
    /// The world's seed, the same each time the world is made: for draws that come out the same when a step is
    /// made again (mix in the object's position).
    /// </summary>
    int Seed { get; }

    /// <summary>Leaves an object of <see cref="Objects"/> out: it is not made.</summary>
    /// <param name="obj">The object.</param>
    void Remove(ICopy obj);

    /// <summary>Adds an object, made with the batch; set its values on the copy returned.</summary>
    /// <param name="prefab">Its prefab's name (an unknown one is left out, said in the editor's log).</param>
    /// <param name="position">Where it stands, world metres.</param>
    /// <param name="rotation">Its turn, Unity Euler degrees.</param>
    /// <returns>The new object, to set its values on.</returns>
    ICopy Add(string prefab, Vector3 position, Vector3 rotation);

    /// <summary>The biome at a point, as the game's Heightmap.Biome value (1 Meadows, 2 Swamp, 4 Mountain...).</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    int Biome(float x, float z);

    /// <summary>The ground's height at a point, in metres, as the step leaves it.</summary>
    /// <param name="x">World x, in metres.</param>
    /// <param name="z">World z, in metres.</param>
    float GroundHeight(float x, float z);
}
