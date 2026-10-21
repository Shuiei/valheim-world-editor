using System.Collections.Generic;
using System.Numerics;
using NVector3 = System.Numerics.Vector3;

namespace ValheimGen;

/// <summary>
/// One object of a blueprint as Expand World Data spawns it (its BlueprintObject): from the blueprint's centre, turned
/// and scaled with it; made by chance (drawn only below 1), with data of its own. A part whose prefab is another
/// blueprint spawns that blueprint's parts in its place.
/// </summary>
/// <param name="Prefab">The prefab, or another blueprint's name.</param>
/// <param name="Position">Its place from the blueprint's centre (x, y, z), metres.</param>
/// <param name="Rotation">Its turn in the blueprint's frame.</param>
/// <param name="Scale">Its own scale on each axis.</param>
/// <param name="Data">Its own data; null for none.</param>
/// <param name="Chance">The chance (0..1) it is made.</param>
public sealed record BlueprintPart(string Prefab, NVector3 Position, Quaternion Rotation, NVector3 Scale, IReadOnlyList<ContentField>? Data, float Chance);
