namespace ExampleVehicles.WorldEditor;

/// <summary>The mod's data keys and prefabs, as its code names them (the same names as in its worldeditor.json).</summary>
public static class Keys
{
    /// <summary>Int: a vehicle's (or a mount's, a post's) own id.</summary>
    public const string Id = "ExampleVehicles_Id";

    /// <summary>Int: the post a cable goes to (its id).</summary>
    public const string Target = "ExampleVehicles_Target";

    /// <summary>Bytes: ids packed the mod's way (a version byte, then each id as four bytes).</summary>
    public const string Packed = "ExampleVehicles_Packed";

    /// <summary>Int: how many corners a sail has (3 or 4).</summary>
    public const string Corners = "ExampleVehicles_Corners";

    /// <summary>Vec3: a sail's corner, in the sail's frame (Corner1 to Corner4).</summary>
    public const string Corner = "ExampleVehicles_Corner";

    /// <summary>Bytes: a sail's colour, red, green, blue and alpha as four bytes.</summary>
    public const string Colour = "ExampleVehicles_Colour";

    /// <summary>The fleet register's prefab: an object the mod keeps its data in (the manifest's "data": nothing to draw).</summary>
    public const string Fleet = "example_fleet";

    /// <summary>The sail's prefab.</summary>
    public const string Sail = "example_sail";

    /// <summary>The post's prefab, cables running from post to post.</summary>
    public const string Post = "example_post";

    /// <summary>The pit's prefab, a hole dug in the ground.</summary>
    public const string Pit = "example_pit";

    /// <summary>Float: a pit's radius, in metres.</summary>
    public const string Radius = "ExampleVehicles_Radius";
}
