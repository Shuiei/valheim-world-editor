using TerrainEditor.App;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// A made-up mod, "Example Vehicles", standing in for any mod that links objects into one: its worldeditor.json is in
/// use for every test (ModManifests.Base, set as the tests load), as a mod's own would be once installed. The editor
/// ships nothing for any real mod; its tests use this one.
/// </summary>
public static class ExampleMod
{
    /// <summary>Int data key of a vehicle's (or a mount's) own id.</summary>
    public const string IdKey = "ExampleVehicles_Id";

    /// <summary>Int data key of a part's vehicle (the root's id).</summary>
    public const string VehicleKey = "ExampleVehicles_Vehicle";

    /// <summary>Int data key of the mount a part is on (a part others hang on; its offset is then in the mount's frame).</summary>
    public const string MountKey = "ExampleVehicles_Mount";

    /// <summary>Vec3 data key of a part's place in its parent's frame.</summary>
    public const string OffsetKey = "ExampleVehicles_Offset";

    /// <summary>Vec3 data key of a part's turn in its parent's frame, Euler degrees.</summary>
    public const string TurnKey = "ExampleVehicles_Turn";

    /// <summary>Quaternion data key of that turn, as older versions of the mod kept it.</summary>
    public const string TurnQuatKey = "ExampleVehicles_TurnQ";

    /// <summary>Int data key naming another object's id (a sail's mast): a reference.</summary>
    public const string TargetKey = "ExampleVehicles_Target";

    /// <summary>Int data key naming the mount a switch drives: another reference.</summary>
    public const string DrivesKey = "ExampleVehicles_Drives";

    /// <summary>Byte-array data key of ids packed the mod's own way: copies leave it out.</summary>
    public const string PackedKey = "ExampleVehicles_Packed";

    /// <summary>Int data key of what is only aboard a vehicle for now (characters, carts): copies leave it out.</summary>
    public const string AboardKey = "ExampleVehicles_Aboard";

    /// <summary>The prefab of a vehicle's root.</summary>
    public const string Hull = "example_hull";

    /// <summary>The mod's worldeditor.json.</summary>
    public const string Manifest = $$"""
        {
          "version": 1,
          "links": [
            {
              "name": "vehicle",
              "id": "{{IdKey}}",
              "parents": ["{{MountKey}}", "{{VehicleKey}}"],
              "offset": "{{OffsetKey}}",
              "rotation": "{{TurnKey}}",
              "rotationQuaternion": "{{TurnQuatKey}}",
              "storedAt": "root",
              "references": ["{{TargetKey}}", "{{DrivesKey}}"],
              "dropOnCopy": ["{{PackedKey}}", "{{AboardKey}}"]
            }
          ]
        }
        """;

    /// <summary>The mod's link rule, as in use.</summary>
    public static LinkRule Rule => ObjectLinks.Current.Rules.Single(r => r.IdKey == StableHash.Of(IdKey));

    /// <summary>The mod's manifest in use before anything else, for every test.</summary>
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Install() => ModManifests.Base = [Manifest];
}
